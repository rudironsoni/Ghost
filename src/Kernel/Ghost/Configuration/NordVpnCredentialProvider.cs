using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ghost.Kernel.Configuration;

public interface INordVpnCredentialProvider
{
    public NordVpnCredentials? GetCredentials();
    public bool ValidateCredentials(out string? errorMessage);
}

public class NordVpnCredentials
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public List<NordVpnServer> Servers { get; set; } = [];
}

public class NordVpnServer
{
    public required string Host { get; set; }
    public int Port { get; set; } = 80;
}

public class ConfigurationNordVpnCredentialProvider : INordVpnCredentialProvider
{
    private static readonly Action<ILogger, Exception?> _logValidationError =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1, "CredentialValidationFailed"), "NordVPN credential validation failed");

    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationNordVpnCredentialProvider> _logger;

    public ConfigurationNordVpnCredentialProvider(
        IConfiguration configuration,
        ILogger<ConfigurationNordVpnCredentialProvider> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public NordVpnCredentials? GetCredentials()
    {
        IConfigurationSection section = _configuration.GetSection("Ghost:Proxy:NordVPN");
        if (!section.Exists())
        {
            return null;
        }

        string? username = section["Username"];
        string? password = section["Password"];
        IConfigurationSection serversSection = section.GetSection("Servers");

        List<NordVpnServer> servers = [];
        if (serversSection.Exists())
        {
            foreach (IConfigurationSection child in serversSection.GetChildren())
            {
                if (child["Host"] is string host)
                {
                    servers.Add(new NordVpnServer
                    {
                        Host = host,
                        Port = int.TryParse(child["Port"], out int port) ? port : 80
                    });
                }
            }
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return new NordVpnCredentials
        {
            Username = username,
            Password = password,
            Servers = servers
        };
    }

    public bool ValidateCredentials(out string? errorMessage)
    {
        errorMessage = null;
        NordVpnCredentials? credentials = GetCredentials();

        if (credentials == null)
        {
            errorMessage = "NordVPN credentials not configured";
            _logValidationError(_logger, null);
            return false;
        }

        if (string.IsNullOrWhiteSpace(credentials.Username))
        {
            errorMessage = "NordVPN username is required";
            _logValidationError(_logger, null);
            return false;
        }

        if (string.IsNullOrWhiteSpace(credentials.Password))
        {
            errorMessage = "NordVPN password is required";
            _logValidationError(_logger, null);
            return false;
        }

        if (credentials.Servers.Count == 0)
        {
            errorMessage = "At least one NordVPN server must be configured";
            _logValidationError(_logger, null);
            return false;
        }

        return true;
    }
}
