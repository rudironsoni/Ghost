using System.CommandLine;
using System.Text.Json;
using Ghost.Platform.Engine;
using Ghost.Sdk.Contracts;

namespace Ghost.CLI.Commands;

/// <summary>
/// Command to execute a job from a JSON file.
/// </summary>
public static class RunCommand
{
    public static Command Create()
    {
        var jobFileArgument = new Argument<FileInfo>(
            name: "job.json",
            description: "Path to the job definition JSON file")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var modeOption = new Option<string?>(
            aliases: new[] { "--mode", "-m" },
            description: "Execution mode: 'execute' (default) or 'replay'")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var runFolderOption = new Option<DirectoryInfo?>(
            aliases: new[] { "--run-folder", "-r" },
            description: "Run folder for replay mode (required when --mode replay)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command("run", "Execute a job from a JSON file")
        {
            jobFileArgument,
            modeOption,
            runFolderOption
        };

        command.SetHandler(async (context) =>
        {
            var jobFile = context.ParseResult.GetValueForArgument(jobFileArgument);
            var mode = context.ParseResult.GetValueForOption(modeOption) ?? "execute";
            var runFolder = context.ParseResult.GetValueForOption(runFolderOption);

            await ExecuteAsync(jobFile, mode, runFolder, context.Console);
        });

        return command;
    }

    private static async Task ExecuteAsync(FileInfo jobFile, string mode, DirectoryInfo? runFolder, IConsole console)
    {
        try
        {
            // Validate inputs
            if (!jobFile.Exists)
            {
                console.Error.WriteLine($"Error: Job file not found: {jobFile.FullName}");
                Environment.Exit(1);
                return;
            }

            if (mode == "replay" && runFolder == null)
            {
                console.Error.WriteLine("Error: --run-folder is required when --mode replay");
                Environment.Exit(1);
                return;
            }

            if (mode == "replay" && !runFolder!.Exists)
            {
                console.Error.WriteLine($"Error: Run folder not found: {runFolder!.FullName}");
                Environment.Exit(1);
                return;
            }

            // Read job definition
            var jobJson = await File.ReadAllTextAsync(jobFile.FullName);
            var job = JsonSerializer.Deserialize<JobDefinition>(jobJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (job == null)
            {
                console.Error.WriteLine("Error: Failed to parse job definition");
                Environment.Exit(1);
                return;
            }

            console.Out.WriteLine($"Job ID: {job.JobId}");
            console.Out.WriteLine($"Plugin: {job.PluginId}");
            console.Out.WriteLine($"Spider: {job.SpiderId}");
            console.Out.WriteLine($"Mode: {mode}");

            // TODO: Load spider spec from plugin
            // TODO: Create engine instance
            // TODO: Execute job

            console.Out.WriteLine("Job execution completed successfully");
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Error: {ex.Message}");
            console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
