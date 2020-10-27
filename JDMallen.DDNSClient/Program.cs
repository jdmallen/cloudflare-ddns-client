using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JDMallen.DDNSClient
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = CreateHostBuilder(args);
			var host = builder.Build(); // Separated for ease of inspection
			host.Run();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.UseSystemd()
				.ConfigureAppConfiguration(
					builder =>
					{
						builder.AddUserSecrets(Assembly.GetExecutingAssembly(), true);
					})
				.ConfigureServices(
					(hostContext, services) =>
					{
						services.AddHostedService<Worker>();
						services.Configure<Settings>(hostContext.Configuration.GetSection(
							"Settings"));
						services.AddSingleton(
							provider =>
							{
								var httpClient = new HttpClient();
								var header = new ProductHeaderValue(
									"JDMallen.DDNSClient",
									Assembly.GetExecutingAssembly()
										.GetName()
										.Version?.ToString());
								var userAgent = new ProductInfoHeaderValue(header);
								httpClient.DefaultRequestHeaders.UserAgent.Add(userAgent);
								return httpClient;
							});
					});
	}
}
