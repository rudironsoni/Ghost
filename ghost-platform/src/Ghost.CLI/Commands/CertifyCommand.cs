using System.CommandLine;
using System.Text.Json;
using Ghost.Sdk.Certification;
using Ghost.Sdk.Contracts;

namespace Ghost.CLI.Commands;

/// <summary>
/// Command to certify a plugin.
/// </summary>
public static class CertifyCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] ModeAliases = { "--mode", "-m" };
    private static readonly string[] FixturesAliases = { "--fixtures", "-f" };
    private static readonly string[] TimeoutAliases = { "--timeout", "-t" };

    public static Command Create()
    {
        var pluginPathArgument = new Argument<DirectoryInfo>(
            name: "pluginPath",
            description: "Path to the plugin directory containing manifest.json")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var modeOption = new Option<string>(
            aliases: ModeAliases,
            description: "Certification mode: offline (default), semi-offline, or live-smoke",
            getDefaultValue: () => "offline")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var fixturesOption = new Option<DirectoryInfo?>(
            aliases: FixturesAliases,
            description: "Path to fixtures directory (default: pluginPath/fixtures)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var timeoutOption = new Option<int>(
            aliases: TimeoutAliases,
            description: "Timeout in seconds (default: 300)",
            getDefaultValue: () => 300)
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command("certify", "Run certification for a plugin")
        {
            pluginPathArgument,
            modeOption,
            fixturesOption,
            timeoutOption
        };

        command.SetHandler(async (context) =>
        {
            var pluginPath = context.ParseResult.GetValueForArgument(pluginPathArgument);
            var mode = context.ParseResult.GetValueForOption(modeOption) ?? "offline";
            var fixtures = context.ParseResult.GetValueForOption(fixturesOption);
            var timeout = context.ParseResult.GetValueForOption(timeoutOption);

            await ExecuteAsync(pluginPath, mode, fixtures, timeout, context.Console);
        });

        return command;
    }

    private static async Task ExecuteAsync(DirectoryInfo pluginPath, string mode, DirectoryInfo? fixtures, int timeout, IConsole console)
    {
        try
        {
            // Validate inputs
            if (!pluginPath.Exists)
            {
                Console.Error.WriteLine($"Error: Plugin path not found: {pluginPath.FullName}");
                Environment.Exit(1);
                return;
            }

            var manifestPath = Path.Combine(pluginPath.FullName, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                Console.Error.WriteLine($"Error: manifest.json not found in {pluginPath.FullName}");
                Environment.Exit(1);
                return;
            }

            var fixturesPath = fixtures?.FullName ?? Path.Combine(pluginPath.FullName, "fixtures");
            if (!Directory.Exists(fixturesPath))
            {
                Console.Error.WriteLine($"Error: Fixtures directory not found: {fixturesPath}");
                Environment.Exit(1);
                return;
            }

            // Parse mode
            var certificationMode = mode.ToLowerInvariant() switch
            {
                "offline" => CertificationMode.Offline,
                "semi-offline" => CertificationMode.SemiOffline,
                "live-smoke" => CertificationMode.LiveSmoke,
                _ => throw new ArgumentException($"Invalid mode: {mode}. Valid modes: offline, semi-offline, live-smoke")
            };

            // Read manifest
            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, JsonOptions);

            if (manifest == null)
            {
                Console.Error.WriteLine("Error: Failed to parse plugin manifest");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Plugin: {manifest.PluginId}");
            Console.WriteLine($"Version: {manifest.Version}");
            Console.WriteLine($"Mode: {mode}");
            Console.WriteLine($"Fixtures: {fixturesPath}");
            Console.WriteLine($"Timeout: {timeout}s");

            // Create certification options
            var options = new CertificationOptions(
                certificationMode,
                fixturesPath,
                MockServerUrl: null,
                TimeSpan.FromSeconds(timeout));

            // TODO: Create certification harness instance
            // TODO: Run certification
            // TODO: Display results

            Console.WriteLine("Certification completed successfully");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
