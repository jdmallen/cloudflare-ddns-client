# Cloudflare Dynamic DNS Client

This is a .NET Core 3.1 Hosted Service that polls https://www.ipify.org/ for your public-facing IP and updates any number of Cloudflare DNS records you nominate.

The service primarily pulls the last known IP address from memory, but it also writes to and reads from `lastIp.cfg` in the executable directory, in the event the service is restarted. This is to limit the number of times Cloudflare is updated.

## Warning
This program sends API messages automatically to Cloudflare. I'm obviously not going to screw with people's DNS records for funsies, but I encourage you to glance through `Worker.cs` and other files to get a sense of what this program does. **USE AT YOUR OWN RISK**

## Quick start
1. Install [.NET Core runtime](https://dotnet.microsoft.com/download) on your target machine, whether it's Windows, Linux, or Mac.
2. Build & publish for your target environment, or download the latest Release for your system, then deploy to your target machine.
3. Set `DOTNET_ENVIRONMENT` environment variable to `Production` on target machine.
4. (Optional) Install as a Windows Service [using sc.exe](https://support.microsoft.com/en-au/help/251192/how-to-create-a-windows-service-by-using-sc-exe) or as [Systemd service](https://dejanstojanovic.net/aspnet/2018/june/setting-up-net-core-servicedaemon-on-linux-os/) (skip to "Service/Daemon setup"). There's a guide to the Linux installation down below.
5. Start it.

## Configuration

Settings for this app are stored in `appsettings.json`. There are some boilerplate (fake) values in there already to give you a sense of how to set it up. 

You can set up one or more Cloudflare zones, each with one or more records you wish to update with your new public IP address.

Zone and Record objects each have a `CanUpdate` flag, which allows the service to call the Cloudflare API to update that zone or record when set to `true`. The zone setting overrides any child record setting.

You can leave the `CloudflareApiBaseUrl` to its default. It's correct as of 2020-10-26.

Change `CloudflareApiToken` to your application token from your account, and each of the IDs to the IDs for your specific zones and records.

You can look up your zone fairly easily by calling `curl -L -X GET 'https://api.cloudflare.com/client/v4/zones' -H 'Authorization: Bearer {your_api_token}'` where `your_api_token` is replaced with the one from your account. Similarly, you can fetch the IDs of your DNS records for a particular zone by calling `curl -L -X GET 'https://api.cloudflare.com/client/v4/zones/{zone_id}/dns_records' -H 'Authorization: Bearer {your_api_token}'` where `zone_id` and `your_api_token` are appropriately replaced. I may eventually bake this into the service as an option. Shall see.

A more detailed breakdown of the configuration options can be found in the schema file at the root of the repo. The rest is pretty self-explanatory.

## Run as Linux Systemd service

These instructions assume you're using an Ubuntu/Debian-based system. Adjust as necessary for your distribution. Any will work, so long as dotnet is installed and it's 64-bit.

1. Create a dedicated user under which the service will Run. I created a user called "dotnetuser" in the "dotnetuser" group using the adduser command: `sudo adduser dotnetuser`.
2. Extract the linux x64 release to `/var/dotnet/ddnsclient/`. You can place it wherever you like, but this is where I put it, and where the rest of the instructions will assume you put it.
3. Modify settings in `appsettings.json` to your preferences.
4. Change the ownership of the folder, and all files within to dotnetuser: `sudo chown dotnetuser:dotnetuser /var/dotnet/ddnsclient && sudo chown dotnetuser:dotnetuser /var/dotnet/ddnsclient/*`
5. Add execute bit to the main binary: `sudo chmod +x /var/dotnet/ddnsclient/JDMallen.DDNSClient`
6. Create a service file here: `/etc/systemd/system/dotnet-ddnsclient.service` using your favorite text editor with elevated privileges. I used vim: `sudo vim /etc/systemd/system/dotnet-ddnsclient.service`.
7. Paste in the below service definition. Be sure to replace "{your_Cloudflare_API_token}", then tweak to your liking. It's critical it remain of type "notify"!
```
[Unit]
Description=Dynamic DNS client for Cloudflare

[Service]
Type=notify
ExecStart= /var/dotnet/ddnsclient/JDMallen.DDNSClient
WorkingDirectory=/var/dotnet/ddnsclient
User=dotnetuser
Group=dotnetuser
Restart=on-failure
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=%n
PrivateTmp=true
Environment=DOTNET_ENVIRONMENT=Production
Environment=Settings__CloudflareApiToken={your_Cloudflare_API_token}

[Install]
WantedBy=multi-user.target
```
8. Reload systemctl config to load this service: `sudo systemctl daemon-reload`
9. Start the service: `sudo systemctl start dotnet-ddnsclient.service`. Note that the service will always start by setting your server fan control to Automatic mode, to establish a sort of baseline of which mode it's in. Assuming it's not above threshold, it'll quiet down your server after {BackToManualThresholdInSeconds} seconds.
10. Monitor its status: `sudo systemctl status dotnet-ddnsclient.service` or `sudo journalctl -u dotnet-ddnsclient.service -f`
11. Check Cloudflare to see if it worked!

Enjoy. :)
