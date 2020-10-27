using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JDMallen.DDNSClient
{
	public class Worker : BackgroundService
	{
		private readonly ILogger<Worker> _logger;
		private readonly Settings _settings;
		private readonly HttpClient _httpClient;
		private IPAddress _lastPublicIp;

		public Worker(
			IOptions<Settings> settings,
			ILogger<Worker> logger,
			HttpClient httpClient)
		{
			_logger = logger;
			_httpClient = httpClient;
			_lastPublicIp = null;
			_settings = settings.Value;
		}

		/// <summary>
		/// This method is called when the <see cref="T:Microsoft.Extensions.Hosting.IHostedService" /> starts. The implementation should return a task that represents
		/// the lifetime of the long running operation(s) being performed.
		/// </summary>
		/// <param name="stoppingToken">Triggered when <see cref="M:Microsoft.Extensions.Hosting.IHostedService.StopAsync(System.Threading.CancellationToken)" /> is called.</param>
		/// <returns>A <see cref="T:System.Threading.Tasks.Task" /> that represents the long running operations.</returns>
		protected override async Task ExecuteAsync(
			CancellationToken stoppingToken)
		{
			_logger.LogInformation($"Detected OS: {_settings.Platform:G}.");

			while (!stoppingToken.IsCancellationRequested)
			{
				var (hasChanged, currentIp) = await HasIpChanged(stoppingToken);
				if (hasChanged)
				{
					_logger.LogWarning($"Public IP changed: {currentIp}. Updating Cloudflare for all zones");
					await UpdateCloudflare(currentIp);
				}
				else
				{
					_logger.LogInformation(
						$"Public IP has not changed: {currentIp}");
				}

				await Delay(stoppingToken);
			}
		}

		private async Task<(bool, IPAddress)> HasIpChanged(
			CancellationToken stoppingToken)
		{
			_logger.LogInformation("Checking if IP changed");
			IPAddress currentPublicIp;
			try
			{
				currentPublicIp = await GetCurrentPublicIp();
			}
			catch
			{
				_logger.LogError("Unable to determine if IP changed");
				return (false, _lastPublicIp);
			}

			if (_lastPublicIp != null
			    && currentPublicIp.Equals(_lastPublicIp))
			{
				return (false, currentPublicIp);
			}

			if (_lastPublicIp == null)
			{
				string configFileIp;
				try
				{
					configFileIp = await File.ReadAllTextAsync(
						"lastIp.cfg",
						stoppingToken);
				}
				catch (Exception)
				{
					_logger.LogWarning("IP file not found. Generating...");
					WriteIpToFile(currentPublicIp, stoppingToken);
					return (true, currentPublicIp);
				}

				var successfulParse = IPAddress.TryParse(
					configFileIp,
					out var parsedConfigFileIp);
				if (successfulParse
				    && currentPublicIp.Equals(parsedConfigFileIp))
				{
					return (false, currentPublicIp);
				}
			}

			_lastPublicIp = currentPublicIp;
			WriteIpToFile(currentPublicIp, stoppingToken);

			return (true, currentPublicIp);
		}

		private void WriteIpToFile(
			IPAddress currentPublicIp,
			CancellationToken stoppingToken)
		{
#pragma warning disable 4014
			File.WriteAllTextAsync(
					"lastIp.cfg",
					currentPublicIp.ToString(),
					stoppingToken)
				.ContinueWith(
#pragma warning restore 4014
					result => { _logger.LogInformation("lastIp.cfg written"); },
					stoppingToken);
		}

		private async Task<IPAddress> GetCurrentPublicIp()
		{
			try
			{
				var response =
					await _httpClient.GetAsync("https://api.ipify.org");

				string body = null;
				if (response.IsSuccessStatusCode)
				{
					body = await response.Content.ReadAsStringAsync();
				}

				if (!string.IsNullOrWhiteSpace(body))
				{
					var parsedIpAddress = IPAddress.Parse(body);
					return parsedIpAddress;
				}

				var invalidEx =
					new InvalidDataException("Response body cannot be empty");
				_logger.LogError(invalidEx, "Cannot parse HTTP response");
				throw invalidEx;
			}
			catch (Exception exception)
			{
				_logger.LogError(exception, "Fetching public IP failed");
				throw;
			}
		}

		private async Task UpdateCloudflare(IPAddress newIp)
		{
			var updateCount = 0;
			foreach (var zone in _settings.CloudflareZones.Where(zone => zone.CanUpdate))
			{
				foreach (var record in zone.Records.Where(record => record.CanUpdate))
				{
					var updateUri =
						$"{_settings.CloudflareApiBaseUrl}/zones/{zone.ID}/dns_records/{record.ID}";
					var request = new HttpRequestMessage(
						HttpMethod.Patch,
						updateUri);
					request.Headers.Authorization =
						new AuthenticationHeaderValue(
							"Bearer",
							_settings.CloudflareApiToken);
					request.Content = new StringContent(
						JsonSerializer.Serialize(
							new {content = newIp.ToString()}),
						Encoding.UTF8,
						"application/json");
					try
					{
						_logger.LogInformation(
							$"Updating DDNS for zone {zone.Name}, "
							+ $"record {record.Name} to {newIp}");
						var response = await _httpClient.SendAsync(request);
						if (!response.IsSuccessStatusCode)
						{
							throw new Exception();
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(
							ex,
							"Request to update Cloudflare for zone "
							+ $"{zone.Name}, record {record.Name} failed");
					}
					updateCount++;
				}
			}

			if (updateCount == 0)
			{
				_logger.LogWarning("Nothing to update! See configuration");
			}
		}

		private async Task Delay(
			CancellationToken cancellationToken)
		{
			await Task.Delay(
				TimeSpan.FromSeconds(_settings.PollingIntervalInSeconds),
				cancellationToken);
		}
	}
}
