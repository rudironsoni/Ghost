namespace Ghost.Resilience;

/// <summary>
/// Thread-safe circuit breaker implementation to prevent cascading failures.
/// </summary>
public sealed class CircuitBreaker : ICircuitBreaker
{
    private readonly CircuitBreakerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly object _lock = new();
    private CircuitState _state;
    private DateTime _stateEnteredAt;
    private int _failureCount;
    private int _successCount;
    private int _consecutiveFailures;
    private int _halfOpenAttempts;
    private int _halfOpenSuccesses;
    private DateTime _lastFailure;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreaker"/> class.
    /// </summary>
    /// <param name="platform">The platform name for this circuit breaker.</param>
    /// <param name="options">The circuit breaker options.</param>
    public CircuitBreaker(string platform, CircuitBreakerOptions options)
    {
        if (string.IsNullOrWhiteSpace(platform))
            throw new ArgumentException("Platform name cannot be empty.", nameof(platform));

        ArgumentNullException.ThrowIfNull(options);

        if (options.FailureThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "FailureThreshold must be greater than zero.");

        if (options.HalfOpenMaxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "HalfOpenMaxAttempts must be greater than zero.");

        if (options.Timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Timeout cannot be negative.");

        Platform = platform;
        _options = options;
        _timeProvider = options.TimeProvider ?? TimeProvider.System;
        _state = CircuitState.Closed;
        _stateEnteredAt = _timeProvider.GetUtcNow().UtcDateTime;
        _lastFailure = DateTime.MinValue;
    }

    /// <summary>
    /// Gets the platform name associated with this circuit breaker.
    /// </summary>
    public string Platform { get; }

    /// <summary>
    /// Gets the current circuit state.
    /// </summary>
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Occurs when the circuit state changes.
    /// </summary>
    public event EventHandler<CircuitStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Executes the provided action with circuit breaker protection.
    /// </summary>
    /// <typeparam name="T">The type of the action result.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <returns>The action result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the circuit is open.</exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!TryEnterExecution())
            throw new InvalidOperationException($"Circuit breaker is open for platform '{Platform}'.");

        try
        {
            T? result = await action.Invoke().ConfigureAwait(false);
            RecordSuccess();
            return result;
        }
        catch
        {
            RecordFailure();
            throw;
        }
    }

    /// <summary>
    /// Gets a snapshot of circuit breaker metrics.
    /// </summary>
    /// <returns>The current metrics snapshot.</returns>
    public CircuitBreakerMetrics GetMetrics()
    {
        lock (_lock)
        {
            return new CircuitBreakerMetrics
            {
                FailureCount = _failureCount,
                SuccessCount = _successCount,
                LastFailure = _lastFailure,
                TimeInCurrentState = _timeProvider.GetUtcNow().UtcDateTime - _stateEnteredAt
            };
        }
    }

    /// <summary>
    /// Creates a circuit breaker configured for LinkedIn.
    /// </summary>
    /// <returns>A LinkedIn circuit breaker instance.</returns>
    public static ICircuitBreaker CreateForLinkedIn() =>
        new CircuitBreaker("LinkedIn", new CircuitBreakerOptions
        {
            FailureThreshold = 5,
            Timeout = TimeSpan.FromMinutes(5),
            HalfOpenMaxAttempts = 3
        });

    /// <summary>
    /// Creates a circuit breaker configured for Indeed.
    /// </summary>
    /// <returns>An Indeed circuit breaker instance.</returns>
    public static ICircuitBreaker CreateForIndeed() =>
        new CircuitBreaker("Indeed", new CircuitBreakerOptions
        {
            FailureThreshold = 10,
            Timeout = TimeSpan.FromMinutes(3),
            HalfOpenMaxAttempts = 5
        });

    /// <summary>
    /// Creates a circuit breaker configured for proxy failover.
    /// </summary>
    /// <returns>A proxy circuit breaker instance.</returns>
    public static ICircuitBreaker CreateForProxy() =>
        new CircuitBreaker("Proxy", new CircuitBreakerOptions
        {
            FailureThreshold = 3,
            Timeout = TimeSpan.FromSeconds(30),
            HalfOpenMaxAttempts = 1
        });

    private bool TryEnterExecution()
    {
        CircuitStateChangedEventArgs? args = null;

        lock (_lock)
        {
            if (_state == CircuitState.Open)
            {
                TimeSpan elapsed = _timeProvider.GetUtcNow().UtcDateTime - _stateEnteredAt;
                if (elapsed >= _options.Timeout)
                {
                    args = TransitionTo(CircuitState.HalfOpen);
                }
                else
                {
                    return false;
                }
            }

            if (_state == CircuitState.HalfOpen)
            {
                if (_halfOpenAttempts >= _options.HalfOpenMaxAttempts)
                {
                    return false;
                }

                _halfOpenAttempts++;
            }
        }

        RaiseStateChanged(args);
        return true;
    }

    private void RecordSuccess()
    {
        CircuitStateChangedEventArgs? args = null;

        lock (_lock)
        {
            _successCount++;

            if (_state == CircuitState.HalfOpen)
            {
                _halfOpenSuccesses++;
                if (_halfOpenSuccesses >= _options.HalfOpenMaxAttempts)
                {
                    args = TransitionTo(CircuitState.Closed);
                }
            }
            else if (_state == CircuitState.Closed)
            {
                _consecutiveFailures = 0;
            }
        }

        RaiseStateChanged(args);
    }

    private void RecordFailure()
    {
        CircuitStateChangedEventArgs? args = null;

        lock (_lock)
        {
            _failureCount++;
            _lastFailure = _timeProvider.GetUtcNow().UtcDateTime;

            if (_state == CircuitState.HalfOpen)
            {
                args = TransitionTo(CircuitState.Open);
            }
            else if (_state == CircuitState.Closed)
            {
                _consecutiveFailures++;
                if (_consecutiveFailures >= _options.FailureThreshold)
                {
                    args = TransitionTo(CircuitState.Open);
                }
            }
        }

        RaiseStateChanged(args);
    }

    private CircuitStateChangedEventArgs? TransitionTo(CircuitState newState)
    {
        if (_state == newState)
            return null;

        CircuitState previous = _state;
        _state = newState;
        _stateEnteredAt = _timeProvider.GetUtcNow().UtcDateTime;

        if (newState == CircuitState.Closed)
        {
            _consecutiveFailures = 0;
            _halfOpenAttempts = 0;
            _halfOpenSuccesses = 0;
        }
        else if (newState == CircuitState.HalfOpen)
        {
            _halfOpenAttempts = 0;
            _halfOpenSuccesses = 0;
        }
        else if (newState == CircuitState.Open)
        {
            _halfOpenAttempts = 0;
            _halfOpenSuccesses = 0;
        }

        return new CircuitStateChangedEventArgs(previous, newState, Platform, _timeProvider.GetUtcNow().UtcDateTime);
    }

    private void RaiseStateChanged(CircuitStateChangedEventArgs? args)
    {
        if (args == null)
            return;

        EventHandler<CircuitStateChangedEventArgs>? handler = StateChanged;
        handler?.Invoke(this, args);
    }
}
