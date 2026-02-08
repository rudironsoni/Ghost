using Ghost.Worker;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

// Configure worker settings from environment variables
var config = new WorkerConfiguration
{
    WorkerId = Environment.GetEnvironmentVariable("WORKER_ID") ?? Environment.MachineName,
    NodeName = Environment.GetEnvironmentVariable("NODE_NAME") ?? Environment.MachineName,
    RedisConnectionString = GetRedisConnectionString(),
    RedisQueueKey = Environment.GetEnvironmentVariable("REDIS_QUEUE_KEY") ?? "ghost:jobs:queue",
    MaxConcurrentJobs = int.TryParse(Environment.GetEnvironmentVariable("MAX_CONCURRENT_JOBS"), out var maxJobs) ? maxJobs : 5,
    PollIntervalMs = int.TryParse(Environment.GetEnvironmentVariable("POLL_INTERVAL_MS"), out var pollMs) ? pollMs : 1000,
    ResultsExpirationHours = int.TryParse(Environment.GetEnvironmentVariable("RESULTS_EXPIRATION_HOURS"), out var expHours) ? expHours : 24
};

builder.Services.AddSingleton(config);

// Configure Redis connection
var redisOptions = ConfigurationOptions.Parse(config.RedisConnectionString);
redisOptions.AbortOnConnectFail = false;
redisOptions.ConnectTimeout = 5000;
redisOptions.SyncTimeout = 5000;

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions));

// Register the worker
builder.Services.AddHostedService<ScraperWorker>();

var host = builder.Build();
host.Run();

static string GetRedisConnectionString()
{
    var host = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
    var password = Environment.GetEnvironmentVariable("REDIS_PASSWORD");

    var connectionString = $"{host}:{port}";
    if (!string.IsNullOrEmpty(password))
    {
        connectionString += $",password={password}";
    }

    return connectionString;
}
