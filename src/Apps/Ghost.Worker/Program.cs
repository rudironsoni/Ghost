using Ghost.Redis;
using Ghost.Worker;
using StackExchange.Redis;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure worker settings from environment variables
var config = new WorkerConfiguration
{
    WorkerId = Environment.GetEnvironmentVariable("WORKER_ID") ?? Environment.MachineName,
    NodeName = Environment.GetEnvironmentVariable("NODE_NAME") ?? Environment.MachineName,
    RedisConnectionString = GetRedisConnectionString(),
    RedisQueueKey = Environment.GetEnvironmentVariable("REDIS_QUEUE_KEY") ?? "ghost:jobs:queue",
    MaxConcurrentJobs = int.TryParse(Environment.GetEnvironmentVariable("MAX_CONCURRENT_JOBS"), out int maxJobs) ? maxJobs : 5,
    PollIntervalMs = int.TryParse(Environment.GetEnvironmentVariable("POLL_INTERVAL_MS"), out int pollMs) ? pollMs : 1000,
    ResultsExpirationHours = int.TryParse(Environment.GetEnvironmentVariable("RESULTS_EXPIRATION_HOURS"), out int expHours) ? expHours : 24
};

builder.Services.AddSingleton(config);

// Configure Redis connection
var redisOptions = ConfigurationOptions.Parse(config.RedisConnectionString);
redisOptions.AbortOnConnectFail = false;
redisOptions.ConnectTimeout = 5000;
redisOptions.SyncTimeout = 5000;

// Use async factory pattern to avoid sync-over-async in DI
builder.Services.AddSingleton<RedisConnectionFactory>(_ => new RedisConnectionFactory(redisOptions));
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    // Defer connection until first use via async lazy pattern
    RedisConnectionFactory factory = sp.GetRequiredService<RedisConnectionFactory>();
    // This will be called asynchronously by hosted services during startup
    return factory.ConnectAsync().GetAwaiter().GetResult();
});

// Register the worker
builder.Services.AddHostedService<ScraperWorker>();

IHost host = builder.Build();
host.Run();

static string GetRedisConnectionString()
{
    string host = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
    string port = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
    string? password = Environment.GetEnvironmentVariable("REDIS_PASSWORD");

    string connectionString = $"{host}:{port}";
    if (!string.IsNullOrEmpty(password))
    {
        connectionString += $",password={password}";
    }

    return connectionString;
}
