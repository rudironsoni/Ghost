using Ghost.Sdk.Spider.Pipeline.Contracts;

namespace Ghost.Sdk.Spider.Pipeline.Middleware;

/// <summary>
/// Middleware that implements the circuit breaker pattern to prevent cascading failures.
/// </summary>
/// <remarks>
/// <para>
/// The circuit breaker pattern protects the system from repeated failures by "opening the circuit"
/// when the failure rate exceeds a threshold. When open, requests fail fast instead of waiting
/// for timeouts, allowing the downstream system time to recover.
/// </para>
/// <para>
/// States:
/// - Closed: Normal operation, requests pass through
/// - Open: Failure threshold exceeded, requests fail immediately
/// - Half-Open: Testing if the system has recovered
/// </para>
/// <para>
/// Configuration keys:
/// - FailureThreshold: Number of failures before opening circuit (default: 5)
/// - SuccessThreshold: Successes needed in half-open to close (default: 2)
/// - Timeout: Time to wait before trying half-open (default: 60 seconds)
/// - SamplingDuration: Window for counting failures (default: 30 seconds)
/// </para>
/// </remarks>
public sealed class CircuitBreakerMiddleware : IPipelineMiddleware
{
    private readonly int _failureThreshold;
    private readonly int _successThreshold;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _samplingDuration;
    private readonly object _lock = new();

    private CircuitState _state;
    private int _failureCount;
    private int _successCount;
    private DateTime _lastFailureTime;
    private DateTime _stateChangedTime;
    private readonly Queue<DateTime> _recentFailures;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerMiddleware"/> class.
    /// </summary>
    /// <param name="configuration">The middleware configuration dictionary.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    public CircuitBreakerMiddleware(Dictionary<string, object> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _failureThreshold = configuration.TryGetValue("FailureThreshold", out var ft) && ft is int failureThreshold
            ? failureThreshold
            : 5;

        _successThreshold = configuration.TryGetValue("SuccessThreshold", out var st) && st is int successThreshold
            ? successThreshold
            : 2;

        _timeout = configuration.TryGetValue("Timeout", out var to) && to is int timeout
            ? TimeSpan.FromSeconds(timeout)
            : TimeSpan.FromSeconds(60);

        _samplingDuration = configuration.TryGetValue("SamplingDuration", out var sd) && sd is int samplingDuration
            ? TimeSpan.FromSeconds(samplingDuration)
            : TimeSpan.FromSeconds(30);

        _state = CircuitState.Closed;
        _failureCount = 0;
        _successCount = 0;
        _lastFailureTime = DateTime.MinValue;
        _stateChangedTime = DateTime.UtcNow;
        _recentFailures = new Queue<DateTime>();
    }

    /// <summary>
    /// Invokes the middleware to apply circuit breaker logic.
    /// </summary>
    /// <param name="context">The pipeline context containing the request.</param>
    /// <param name="continuation">The delegate to invoke the next middleware in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the circuit is open.</exception>
    public async Task InvokeAsync(PipelineContext context, PipelineDelegate continuation)
    {
        // Check circuit state before proceeding
        lock (_lock)
        {
            UpdateCircuitState();

            if (_state == CircuitState.Open)
            {
                throw new InvalidOperationException(
                    $"Circuit breaker is open. Service is temporarily unavailable. Last failure: {_lastFailureTime}");
            }
        }

        try
        {
            await continuation(context);

            // Record success
            lock (_lock)
            {
                OnSuccess();
            }
        }
        catch (Exception)
        {
            // Record failure
            lock (_lock)
            {
                OnFailure();
            }

            // Re-throw to propagate the error
            throw;
        }
    }

    /// <summary>
    /// Updates the circuit state based on elapsed time and current state.
    /// Must be called with lock held.
    /// </summary>
    private void UpdateCircuitState()
    {
        if (_state == CircuitState.Open)
        {
            // Check if timeout has elapsed to transition to half-open
            if (DateTime.UtcNow - _stateChangedTime >= _timeout)
            {
                TransitionTo(CircuitState.HalfOpen);
            }
        }

        // Clean up old failures outside the sampling window
        var cutoff = DateTime.UtcNow - _samplingDuration;
        while (_recentFailures.Count > 0 && _recentFailures.Peek() < cutoff)
        {
            _recentFailures.Dequeue();
        }
    }

    /// <summary>
    /// Records a successful request.
    /// Must be called with lock held.
    /// </summary>
    private void OnSuccess()
    {
        if (_state == CircuitState.HalfOpen)
        {
            _successCount++;

            // If we've had enough successes in half-open state, close the circuit
            if (_successCount >= _successThreshold)
            {
                TransitionTo(CircuitState.Closed);
            }
        }
        else if (_state == CircuitState.Closed)
        {
            // Reset failure count on success in closed state
            _failureCount = 0;
        }
    }

    /// <summary>
    /// Records a failed request.
    /// Must be called with lock held.
    /// </summary>
    private void OnFailure()
    {
        _lastFailureTime = DateTime.UtcNow;
        _recentFailures.Enqueue(_lastFailureTime);
        _failureCount++;

        if (_state == CircuitState.HalfOpen)
        {
            // Any failure in half-open state reopens the circuit
            TransitionTo(CircuitState.Open);
        }
        else if (_state == CircuitState.Closed)
        {
            // Check if we've exceeded the failure threshold
            if (_recentFailures.Count >= _failureThreshold)
            {
                TransitionTo(CircuitState.Open);
            }
        }
    }

    /// <summary>
    /// Transitions the circuit to a new state.
    /// Must be called with lock held.
    /// </summary>
    /// <param name="newState">The new circuit state.</param>
    private void TransitionTo(CircuitState newState)
    {
        if (_state == newState)
            return;

        _state = newState;
        _stateChangedTime = DateTime.UtcNow;

        // Reset counters based on new state
        switch (newState)
        {
            case CircuitState.Closed:
                _failureCount = 0;
                _successCount = 0;
                _recentFailures.Clear();
                break;

            case CircuitState.Open:
                _successCount = 0;
                break;

            case CircuitState.HalfOpen:
                _failureCount = 0;
                _successCount = 0;
                break;
        }
    }

    /// <summary>
    /// Represents the state of the circuit breaker.
    /// </summary>
    private enum CircuitState
    {
        /// <summary>
        /// Circuit is closed, requests are passing through normally.
        /// </summary>
        Closed,

        /// <summary>
        /// Circuit is open, requests are failing fast.
        /// </summary>
        Open,

        /// <summary>
        /// Circuit is half-open, testing if the system has recovered.
        /// </summary>
        HalfOpen
    }
}
