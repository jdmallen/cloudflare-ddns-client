using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace JDMallen.DDNSClient
{
	public class Settings
	{
		public Settings()
		{
			CloudflareZones = new List<CloudflareZone>();
		}

		public string CloudflareApiToken { get; set; }

		public string CloudflareApiBaseUrl { get; set; }

		public List<CloudflareZone> CloudflareZones { get; set; }

		public int PollingIntervalInSeconds { get; set; } = 60 * 60 * 12; // 12h

		public Platform Platform
		{
			get
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					return Platform.Windows;
				}

				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					return Platform.Linux;
				}

				throw new PlatformNotSupportedException(
					"Only works on Windows or Linux.");
			}
		}
	}

	public enum Platform
	{
		Linux,
		Windows
	}

	public class CloudflareZone
	{
		public string ID { get; set; }

		public string Name { get; set; }

		public bool CanUpdate { get; set; }

		public ICollection<CloudflareDNSRecord> Records { get; set; }
	}

	public class CloudflareDNSRecord
	{
		public string ID { get; set; }

		public string Name { get; set; }

		public bool CanUpdate { get; set; }
	}
}
