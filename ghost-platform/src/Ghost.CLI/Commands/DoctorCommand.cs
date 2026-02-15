using System.CommandLine;

namespace Ghost.CLI.Commands;

/// <summary>
/// Command to verify the Ghost environment.
/// </summary>
public static class DoctorCommand
{
    public static Command Create()
    {
        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
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
        console.Out.WriteLine("Ghost Environment Doctor");
        console.Out.WriteLine(new string('=', 40));

        var allPassed = true;

        // Check .NET version
        console.Out.Write("\n[1/5] .NET Runtime: ");
        try
        {
            var version = Environment.Version;
            console.Out.WriteLine($"OK ({version})");
            if (verbose)
            {
                console.Out.WriteLine($"  Runtime: {RuntimeInformation.FrameworkDescription}");
                console.Out.WriteLine($"  OS: {RuntimeInformation.OSDescription}");
                console.Out.WriteLine($"  Processor: {RuntimeInformation.ProcessArchitecture}");
            }
        }
        catch (Exception ex)
        {
            console.Out.WriteLine($"FAIL ({ex.Message})");
            allPassed = false;
        }

        // Check working directory
        console.Out.Write("[2/5] Working Directory: ");
        try
        {
            var cwd = Directory.GetCurrentDirectory();
            console.Out.WriteLine($"OK ({cwd})");
            if (verbose)
            {
                console.Out.WriteLine($"  Readable: {Directory.Exists(cwd)}");
                console.Out.WriteLine($"  Writable: {HasWritePermission(cwd)}");
            }
        }
        catch (Exception ex)
        {
            console.Out.WriteLine($"FAIL ({ex.Message})");
            allPassed = false;
        }

        // Check temp directory
        console.Out.Write("[3/5] Temp Directory: ");
        try
        {
            var temp = Path.GetTempPath();
            console.Out.WriteLine($"OK ({temp})");
            if (verbose)
            {
                console.Out.WriteLine($"  Exists: {Directory.Exists(temp)}");
                console.Out.WriteLine($"  Writable: {HasWritePermission(temp)}");
            }
        }
        catch (Exception ex)
        {
            console.Out.WriteLine($"FAIL ({ex.Message})");
            allPassed = false;
        }

        // Check environment variables
        console.Out.Write("[4/5] Environment Variables: ");
        try
        {
            var envVars = new[] { "PATH", "HOME", "USERPROFILE" };
            var missing = envVars.Where(v => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v))).ToList();
            if (missing.Any())
            {
                console.Out.WriteLine($"WARN (Missing: {string.Join(", ", missing)})");
            }
            else
            {
                console.Out.WriteLine("OK");
            }
            if (verbose)
            {
                foreach (var envVar in envVars)
                {
                    var value = Environment.GetEnvironmentVariable(envVar);
                    console.Out.WriteLine($"  {envVar}: {value ?? "(not set)"}");
                }
            }
        }
        catch (Exception ex)
        {
            console.Out.WriteLine($"FAIL ({ex.Message})");
            allPassed = false;
        }

        // Check file system
        console.Out.Write("[5/5] File System: ");
        try
        {
            var testFile = Path.Combine(Path.GetTempPath(), $"ghost-doctor-test-{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            console.Out.WriteLine("OK");
            if (verbose)
            {
                console.Out.WriteLine("  Read/write test passed");
            }
        }
        catch (Exception ex)
        {
            console.Out.WriteLine($"FAIL ({ex.Message})");
            allPassed = false;
        }

        console.Out.WriteLine();
        console.Out.WriteLine(new string('=', 40));
        if (allPassed)
        {
            console.Out.WriteLine("All checks passed!");
            Environment.Exit(0);
        }
        else
        {
            console.Out.WriteLine("Some checks failed. Please fix the issues above.");
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
