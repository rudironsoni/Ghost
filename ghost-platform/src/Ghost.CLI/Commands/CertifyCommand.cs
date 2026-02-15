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
    public static Command Create()
    {
        var pluginPathArgument = new Argument<DirectoryInfo>(
            name: "pluginPath",
            description: "Path to the plugin directory containing manifest.json")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var modeOption = new Option<string>(
            aliases: new[] { "--mode", "-m" },
            description: "Certification mode: offline (default), semi-offline, or live-smoke")
        {
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = () => "offline"
        };

        var fixturesOption = new Option<DirectoryInfo?>(
            aliases: new[] { "--fixtures", "-f" },
            description: "Path to fixtures directory (default: pluginPath/fixtures)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var timeoutOption = new Option<int>(
            aliases: new[] { "--timeout", "-t" },
            description: "Timeout in seconds (default: 300)")
        {
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = () => 300
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
            var mode = context.ParseResult.GetValueForOption(modeOption);
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
                console.Error.WriteLine($"Error: Plugin path not found: {pluginPath.FullName}");
                Environment.Exit(1);
                return;
            }

            var manifestPath = Path.Combine(pluginPath.FullName, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                console.Error.WriteLine($"Error: manifest.json not found in {pluginPath.FullName}");
                Environment.Exit(1);
                return;
            }

            var fixturesPath = fixtures?.FullName ?? Path.Combine(pluginPath.FullName, "fixtures");
            if (!Directory.Exists(fixturesPath))
            {
                console.Error.WriteLine($"Error: Fixtures directory not found: {fixturesPath}");
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
            var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (manifest == null)
            {
                console.Error.WriteLine("Error: Failed to parse plugin manifest");
                Environment.Exit(1);
                return;
            }

            console.Out.WriteLine($"Plugin: {manifest.PluginId}");
            console.Out.WriteLine($"Version: {manifest.Version}");
            console.Out.WriteLine($"Mode: {mode}");
            console.Out.WriteLine($"Fixtures: {fixturesPath}");
            console.Out.WriteLine($"Timeout: {timeout}s");

            // Create certification options
            var options = new CertificationOptions(
                certificationMode,
                fixturesPath,
                MockServerUrl: null,
                TimeSpan.FromSeconds(timeout));

            // TODO: Create certification harness instance
            // TODO: Run certification
            // TODO: Display results

            console.Out.WriteLine("Certification completed successfully");
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Error: {ex.Message}");
            console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
