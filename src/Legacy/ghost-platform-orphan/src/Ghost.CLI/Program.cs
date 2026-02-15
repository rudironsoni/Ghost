using System.CommandLine;
using Ghost.CLI.Commands;

namespace Ghost.CLI;

/// <summary>
/// Ghost CLI - Development and operations tool for the Ghost platform.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Ghost CLI - Development and operations tool")
        {
            Description = "Execute jobs, replay runs, certify plugins, and verify environment"
        };

        // Add subcommands
        rootCommand.AddCommand(RunCommand.Create());
        rootCommand.AddCommand(ReplayCommand.Create());
        rootCommand.AddCommand(CertifyCommand.Create());
        rootCommand.AddCommand(DoctorCommand.Create());

        return await rootCommand.InvokeAsync(args);
    }
}
