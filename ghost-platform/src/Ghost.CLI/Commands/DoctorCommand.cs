using System.CommandLine;
using System.Runtime.InteropServices;

namespace Ghost.CLI.Commands;

/// <summary>
/// Command to verify the Ghost environment.
/// </summary>
public static class DoctorCommand
{
    private static readonly string[] EnvVars = { "PATH", "HOME", "USERPROFILE" };
    private static readonly string[] VerboseAliases = { "--verbose", "-v" };

    public static Command Create()
    {
        var verboseOption = new Option<bool>(
            aliases: VerboseAliases,
            description: "Show detailed diagnostic information")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command("doctor", "Verify Ghost environment and dependencies")
        {
            verboseOption
        };

        command.SetHandler((context) =>
        {
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            Execute(verbose, context.Console);
        });

        return command;
    }

    private static void Execute(bool verbose, IConsole console)
    {
        Console.WriteLine("Ghost Environment Doctor");
        Console.WriteLine(new string('=', 40));

        var allPassed = true;

        // Check .NET version
        Console.Write("\n[1/5] .NET Runtime: ");
        try
        {
            var version = Environment.Version;
            Console.WriteLine($"OK ({version})");
            if (verbose)
            {
                Console.WriteLine($"  Runtime: {RuntimeInformation.FrameworkDescription}");
                Console.WriteLine($"  OS: {RuntimeInformation.OSDescription}");
                Console.WriteLine($"  Processor: {RuntimeInformation.ProcessArchitecture}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL ({ex.Message})");
            allPassed = false;
        }

        // Check working directory
        Console.Write("[2/5] Working Directory: ");
        try
        {
            var cwd = Directory.GetCurrentDirectory();
            Console.WriteLine($"OK ({cwd})");
            if (verbose)
            {
                Console.WriteLine($"  Readable: {Directory.Exists(cwd)}");
                Console.WriteLine($"  Writable: {HasWritePermission(cwd)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL ({ex.Message})");
            allPassed = false;
        }

        // Check temp directory
        Console.Write("[3/5] Temp Directory: ");
        try
        {
            var temp = Path.GetTempPath();
            Console.WriteLine($"OK ({temp})");
            if (verbose)
            {
                Console.WriteLine($"  Exists: {Directory.Exists(temp)}");
                Console.WriteLine($"  Writable: {HasWritePermission(temp)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL ({ex.Message})");
            allPassed = false;
        }

        // Check environment variables
        Console.Write("[4/5] Environment Variables: ");
        try
        {
            var missing = EnvVars.Where(v => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v))).ToList();
            if (missing.Count > 0)
            {
                Console.WriteLine($"WARN (Missing: {string.Join(", ", missing)})");
            }
            else
            {
                Console.WriteLine("OK");
            }
            if (verbose)
            {
                foreach (var envVar in EnvVars)
                {
                    var value = Environment.GetEnvironmentVariable(envVar);
                    Console.WriteLine($"  {envVar}: {value ?? "(not set)"}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL ({ex.Message})");
            allPassed = false;
        }

        // Check file system
        Console.Write("[5/5] File System: ");
        try
        {
            var testFile = Path.Combine(Path.GetTempPath(), $"ghost-doctor-test-{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            Console.WriteLine("OK");
            if (verbose)
            {
                Console.WriteLine("  Read/write test passed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL ({ex.Message})");
            allPassed = false;
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 40));
        if (allPassed)
        {
            Console.WriteLine("All checks passed!");
            Environment.Exit(0);
        }
        else
        {
            Console.WriteLine("Some checks failed. Please fix the issues above.");
            Environment.Exit(1);
        }
    }

    private static bool HasWritePermission(string path)
    {
        try
        {
            var testFile = Path.Combine(path, $".write-test-{Guid.NewGuid()}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
