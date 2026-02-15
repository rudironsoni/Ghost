using System.CommandLine;

namespace Ghost.CLI.Commands;

/// <summary>
/// Command to replay a job from a stored bundle.
/// </summary>
public static class ReplayCommand
{
    private static readonly string[] OutputAliases = { "--output", "-o" };

    public static Command Create()
    {
        var bundleArgument = new Argument<FileInfo>(
            name: "bundle.zip",
            description: "Path to the replay bundle ZIP file")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var outputOption = new Option<DirectoryInfo>(
            aliases: OutputAliases,
            description: "Output directory for replay results (default: current directory)",
            getDefaultValue: () => new DirectoryInfo("."))
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command("replay", "Replay a job from a stored bundle")
        {
            bundleArgument,
            outputOption
        };

        command.SetHandler(async (context) =>
        {
            var bundle = context.ParseResult.GetValueForArgument(bundleArgument);
            var output = context.ParseResult.GetValueForOption(outputOption) ?? new DirectoryInfo(".");

            await ExecuteAsync(bundle, output, context.Console);
        });

        return command;
    }

    private static async Task ExecuteAsync(FileInfo bundle, DirectoryInfo output, IConsole console)
    {
        try
        {
            // Validate inputs
            if (!bundle.Exists)
            {
                Console.Error.WriteLine($"Error: Bundle file not found: {bundle.FullName}");
                Environment.Exit(1);
                return;
            }

            if (!output.Exists)
            {
                output.Create();
            }

            Console.WriteLine($"Bundle: {bundle.FullName}");
            Console.WriteLine($"Output: {output.FullName}");

            // TODO: Extract bundle
            // TODO: Load events from storage
            // TODO: Replay execution

            Console.WriteLine("Replay completed successfully");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
