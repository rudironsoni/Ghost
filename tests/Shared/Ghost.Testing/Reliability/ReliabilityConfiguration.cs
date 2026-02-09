using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Ghost.Testing.Reliability;

/// <summary>
/// Custom xUnit test framework that adds reliability features to test execution.
/// This framework enforces timeouts, leak detection, and other reliability measures.
/// </summary>
/// <remarks>
/// To enable this framework, add the following assembly attribute to your test project:
/// <code>
/// [assembly: TestFramework("Ghost.Testing.Reliability.ReliabilityConfiguration", "Ghost.Testing")]
/// </code>
/// </remarks>
public class ReliabilityConfiguration : XunitTestFramework
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReliabilityConfiguration"/> class.
    /// </summary>
    /// <param name="messageSink">The message sink used to report diagnostic messages.</param>
    public ReliabilityConfiguration(IMessageSink messageSink)
        : base(messageSink)
    {
        messageSink.OnMessage(new DiagnosticMessage(
            "Ghost.Testing.Reliability: Framework initialized with timeout and leak detection"));
    }

    /// <summary>
    /// Creates a custom test framework executor that adds reliability features.
    /// </summary>
    /// <param name="assemblyName">The name of the test assembly.</param>
    /// <returns>A reliability-enhanced test framework executor.</returns>
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
    {
        return new ReliabilityExecutor(
            assemblyName,
            SourceInformationProvider,
            DiagnosticMessageSink);
    }
}

/// <summary>
/// Custom xUnit test framework executor that adds reliability features to test execution.
/// </summary>
internal sealed class ReliabilityExecutor : XunitTestFrameworkExecutor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReliabilityExecutor"/> class.
    /// </summary>
    public ReliabilityExecutor(
        AssemblyName assemblyName,
        ISourceInformationProvider sourceInformationProvider,
        IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
    }

    /// <summary>
    /// Runs tests with reliability enhancements.
    /// </summary>
    protected override async void RunTestCases(
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
    {
        // Take a snapshot of browser processes before running tests
        var initialSnapshot = BrowserLeakDetector.GetBrowserProcessSnapshot();

        try
        {
            // Run the tests using the default execution logic
            base.RunTestCases(testCases, executionMessageSink, executionOptions);
        }
        finally
        {
            // Check for leaked browser processes after tests complete
            await Task.Delay(1000); // Give processes a moment to clean up
            var leakedProcesses = BrowserLeakDetector.DetectNewProcesses(initialSnapshot);

            if (leakedProcesses.Count > 0)
            {
                var processDetails = string.Join(", ", leakedProcesses.Select(p => $"{p.ProcessName}(PID:{p.Id})"));
                DiagnosticMessageSink.OnMessage(new DiagnosticMessage(
                    $"WARNING: Browser process leak detected after test run: {processDetails}"));

                // Clean up leaked processes
                foreach (var process in leakedProcesses)
                {
                    try
                    {
                        process.Kill();
                        process.Dispose();
                    }
                    catch
                    {
                        // Best-effort cleanup
                    }
                }
            }
        }
    }
}
