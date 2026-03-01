using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ghost.Testing.Reliability;

/// <summary>
/// Detects orphaned browser processes that may indicate test cleanup failures.
/// Helps identify resource leaks in browser-based tests.
/// </summary>
public static class BrowserLeakDetector
{
    private static readonly string[] BrowserProcessNames = ["chromium", "chrome", "playwright"];

    /// <summary>
    /// Asserts that no orphaned browser processes are running.
    /// Throws an exception if leaked processes are detected.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when orphaned browser processes are detected.
    /// </exception>
    public static void AssertNoLeaks()
    {
        List<Process> leakedProcesses = DetectLeakedProcesses();

        if (leakedProcesses.Count > 0)
        {
            string processDetails = string.Join(", ", leakedProcesses.Select(p => $"{p.ProcessName}(PID:{p.Id})"));
            throw new InvalidOperationException(
                $"Browser process leak detected: {processDetails}. " +
                "Tests may not be cleaning up browser sessions properly.");
        }
    }

    /// <summary>
    /// Detects potentially leaked browser processes.
    /// </summary>
    /// <returns>A list of processes that may represent browser leaks.</returns>
    public static List<Process> DetectLeakedProcesses()
    {
        List<Process> leakedProcesses = [];

        try
        {
            foreach (string processName in BrowserProcessNames)
            {
                Process[] processes = Process.GetProcessesByName(processName);
                leakedProcesses.AddRange(processes);
            }
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or NotSupportedException)
        {
            // Platform doesn't support process enumeration - skip leak detection
            return leakedProcesses;
        }

        return leakedProcesses;
    }

    /// <summary>
    /// Kills all detected browser processes.
    /// Use with caution - this will terminate ALL browser instances on the system.
    /// </summary>
    /// <param name="force">If true, forcefully kills processes without waiting for graceful shutdown.</param>
    public static void KillAllBrowserProcesses(bool force = false)
    {
        List<Process> processes = DetectLeakedProcesses();

        foreach (Process process in processes)
        {
            try
            {
                if (force)
                {
                    process.Kill();
                }
                else
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                    }
                }
                process.Dispose();
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Process may have already exited or we don't have permissions
                // Continue with next process
            }
        }
    }

    /// <summary>
    /// Gets a snapshot of currently running browser processes for comparison.
    /// Useful for detecting new processes created during a test.
    /// </summary>
    /// <returns>A set of process IDs for currently running browser processes.</returns>
    public static HashSet<int> GetBrowserProcessSnapshot()
    {
        HashSet<int> snapshot = [];
        List<Process> processes = DetectLeakedProcesses();

        foreach (Process process in processes)
        {
            snapshot.Add(process.Id);
            process.Dispose();
        }

        return snapshot;
    }

    /// <summary>
    /// Detects new browser processes created since the snapshot was taken.
    /// </summary>
    /// <param name="snapshot">The process snapshot to compare against.</param>
    /// <returns>A list of newly created browser processes.</returns>
    public static List<Process> DetectNewProcesses(HashSet<int> snapshot)
    {
        List<Process> currentProcesses = DetectLeakedProcesses();
        return currentProcesses.Where(p => !snapshot.Contains(p.Id)).ToList();
    }
}
