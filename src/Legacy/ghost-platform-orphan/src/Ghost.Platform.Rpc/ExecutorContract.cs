using Ghost.Sdk.Contracts;
using System.Text.Json;

namespace Ghost.Platform.Rpc;

/// <summary>
/// Base message type for executor communication.
/// </summary>
public abstract record ExecutorMessageBase
{
    /// <summary>
    /// Message type identifier.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;
}

/// <summary>
/// Handshake request from client to executor.
/// </summary>
public sealed record HandshakeRequest : ExecutorMessageBase
{
    public HandshakeRequest()
    {
        MessageType = "handshake_request";
    }

    /// <summary>
    /// Protocol version being used.
    /// </summary>
    public required string ProtocolVersion { get; init; }

    /// <summary>
    /// Client identifier.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Supported capabilities.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Handshake response from executor to client.
/// </summary>
public sealed record HandshakeResponse : ExecutorMessageBase
{
    public HandshakeResponse()
    {
        MessageType = "handshake_response";
    }

    /// <summary>
    /// Protocol version accepted by executor.
    /// </summary>
    public required string ProtocolVersion { get; init; }

    /// <summary>
    /// Executor identifier.
    /// </summary>
    public required string ExecutorId { get; init; }

    /// <summary>
    /// Whether the handshake was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if handshake failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Supported capabilities by executor.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Request to start a job execution.
/// </summary>
public sealed record StartJobRequest : ExecutorMessageBase
{
    public StartJobRequest()
    {
        MessageType = "start_job_request";
    }

    /// <summary>
    /// Job definition to execute.
    /// </summary>
    public required JobDefinition JobDefinition { get; init; }

    /// <summary>
    /// Plugin manifest for the job.
    /// </summary>
    public required PluginManifest PluginManifest { get; init; }

    /// <summary>
    /// Spider specification for the job.
    /// </summary>
    public required SpiderSpec SpiderSpec { get; init; }
}

/// <summary>
/// Response indicating job was started.
/// </summary>
public sealed record StartJobResponse : ExecutorMessageBase
{
    public StartJobResponse()
    {
        MessageType = "start_job_response";
    }

    /// <summary>
    /// Unique identifier for this execution run.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Whether the job was successfully started.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if job failed to start.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Request to stop a running job.
/// </summary>
public sealed record StopJobRequest : ExecutorMessageBase
{
    public StopJobRequest()
    {
        MessageType = "stop_job_request";
    }

    /// <summary>
    /// Run ID of the job to stop.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Whether to force stop (kill process).
    /// </summary>
    public bool Force { get; init; }
}

/// <summary>
/// Response indicating job was stopped.
/// </summary>
public sealed record StopJobResponse : ExecutorMessageBase
{
    public StopJobResponse()
    {
        MessageType = "stop_job_response";
    }

    /// <summary>
    /// Run ID of the stopped job.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Whether the job was successfully stopped.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if stop failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Message from executor containing events or items.
/// </summary>
public sealed record ExecutorMessage : ExecutorMessageBase
{
    public ExecutorMessage()
    {
        MessageType = "executor_message";
    }

    /// <summary>
    /// Engine event if present.
    /// </summary>
    public EngineEvent? Event { get; init; }

    /// <summary>
    /// Batch of items if present.
    /// </summary>
    public ItemBatch? Items { get; init; }

    /// <summary>
    /// Error if present.
    /// </summary>
    public ExecutorError? Error { get; init; }
}

/// <summary>
/// Batch of items emitted by executor.
/// </summary>
public sealed record ItemBatch
{
    /// <summary>
    /// Run ID that emitted these items.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Job ID that emitted these items.
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Items in the batch.
    /// </summary>
    public required IReadOnlyList<JsonDocument> Items { get; init; }

    /// <summary>
    /// Batch sequence number.
    /// </summary>
    public long SequenceNumber { get; init; }
}

/// <summary>
/// Error information from executor.
/// </summary>
public sealed record ExecutorError
{
    /// <summary>
    /// Error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Stack trace if available.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Additional error details.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Details { get; init; } =
        new Dictionary<string, object?>();
}

/// <summary>
/// Heartbeat message from executor.
/// </summary>
public sealed record HeartbeatMessage : ExecutorMessageBase
{
    public HeartbeatMessage()
    {
        MessageType = "heartbeat";
    }

    /// <summary>
    /// Timestamp of heartbeat.
    /// </summary>
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Active run IDs.
    /// </summary>
    public IReadOnlyList<string> ActiveRuns { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Shutdown request from client.
/// </summary>
public sealed record ShutdownRequest : ExecutorMessageBase
{
    public ShutdownRequest()
    {
        MessageType = "shutdown_request";
    }

    /// <summary>
    /// Whether to force shutdown.
    /// </summary>
    public bool Force { get; init; }

    /// <summary>
    /// Reason for shutdown.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Shutdown response from executor.
/// </summary>
public sealed record ShutdownResponse : ExecutorMessageBase
{
    public ShutdownResponse()
    {
        MessageType = "shutdown_response";
    }

    /// <summary>
    /// Whether shutdown was acknowledged.
    /// </summary>
    public required bool Acknowledged { get; init; }

    /// <summary>
    /// Estimated time until shutdown completes.
    /// </summary>
    public TimeSpan? EstimatedTimeUntilShutdown { get; init; }
}
