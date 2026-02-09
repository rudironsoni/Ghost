using System.Reflection;
using Xunit.Sdk;

namespace Ghost.Testing.Reliability;

/// <summary>
/// Global timeout enforcement attribute for test methods.
/// Prevents test hangs by enforcing a default 10-second timeout.
/// </summary>
/// <remarks>
/// Apply this attribute to test methods to ensure they complete within a reasonable time.
/// Default timeout is 10 seconds, but can be customized per test.
/// Uses xUnit's BeforeAfterTestAttribute to implement timeout enforcement.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class TestTimeoutAttribute : BeforeAfterTestAttribute, IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _timeoutTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestTimeoutAttribute"/> class.
    /// </summary>
    /// <param name="milliseconds">
    /// The maximum time in milliseconds that the test is allowed to run.
    /// Defaults to 10000ms (10 seconds).
    /// </param>
    public TestTimeoutAttribute(int milliseconds = 10000)
    {
        if (milliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "Timeout must be greater than 0");
        }

        TimeoutMilliseconds = milliseconds;
    }

    /// <summary>
    /// Gets the timeout duration in milliseconds.
    /// </summary>
    public int TimeoutMilliseconds { get; }

    /// <summary>
    /// Called before the test method is executed.
    /// Starts the timeout enforcement timer.
    /// </summary>
    public override void Before(MethodInfo methodUnderTest)
    {
        _cts = new CancellationTokenSource();
        _timeoutTask = Task.Delay(TimeoutMilliseconds, _cts.Token);
    }

    /// <summary>
    /// Called after the test method is executed.
    /// Cancels the timeout enforcement timer and throws if timeout was exceeded.
    /// </summary>
    public override void After(MethodInfo methodUnderTest)
    {
        if (_timeoutTask?.IsCompleted == true && !_cts!.IsCancellationRequested)
        {
            // Timeout was reached
            Dispose();
            throw new TimeoutException(
                $"Test '{methodUnderTest.DeclaringType?.Name}.{methodUnderTest.Name}' " +
                $"exceeded timeout of {TimeoutMilliseconds}ms");
        }

        // Cancel and cleanup
        Dispose();
    }

    /// <summary>
    /// Releases the resources used by this attribute.
    /// </summary>
    public void Dispose()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}
