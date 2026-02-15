using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ghost.Captcha;

/// <summary>
/// Extension methods for registering CAPTCHA services
/// </summary>
public static class CaptchaServiceCollectionExtensions
{
    /// <summary>
    /// Adds CAPTCHA solving services with default configuration
    /// Registers NopeCHA as primary and TensorFlow as backup
    /// </summary>
    public static IServiceCollection AddCaptchaSolving(this IServiceCollection services)
    {
        return services.AddCaptchaSolving(options => { });
    }

    /// <summary>
    /// Adds CAPTCHA solving services with custom configuration
    /// </summary>
    public static IServiceCollection AddCaptchaSolving(
        this IServiceCollection services,
        Action<CaptchaOptions> configure)
    {
        var options = new CaptchaOptions();
        configure(options);

        // Register providers in priority order (NopeCHA first, TensorFlow second)
        if (options.EnableNopeCHA)
        {
            services.AddSingleton<ICaptchaProvider>(sp =>
                new NopeCHAProvider(
                    sp.GetRequiredService<ILogger<NopeCHAProvider>>(),
                    options.NopeCHAExtensionPath,
                    options.SolvingTimeout));
        }

        if (options.EnableTensorFlow)
        {
            services.AddHttpClient();
            services.AddSingleton<ICaptchaProvider>(sp =>
            {
                IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                HttpClient httpClient = httpClientFactory.CreateClient();
                return new TensorFlowCaptchaProvider(
                    sp.GetRequiredService<ILogger<TensorFlowCaptchaProvider>>(),
                    httpClient,
                    options.TensorFlowApiEndpoint,
                    options.SolvingTimeout);
            });
        }

        // Register the main service
        services.AddSingleton<CaptchaService>();

        return services;
    }
}

/// <summary>
/// Configuration options for CAPTCHA solving
/// </summary>
public sealed class CaptchaOptions
{
    /// <summary>
    /// Enable NopeCHA provider (browser extension)
    /// Default: true
    /// </summary>
    public bool EnableNopeCHA { get; set; } = true;

    /// <summary>
    /// Enable TensorFlow provider (self-hosted model)
    /// Default: true
    /// </summary>
    public bool EnableTensorFlow { get; set; } = true;

    /// <summary>
    /// Path to NopeCHA browser extension
    /// </summary>
    public string? NopeCHAExtensionPath { get; set; }

    /// <summary>
    /// API endpoint for TensorFlow captcha solver
    /// Default: http://localhost:5000
    /// </summary>
    public string TensorFlowApiEndpoint { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Timeout for CAPTCHA solving attempts
    /// Default: 60 seconds
    /// </summary>
    public TimeSpan SolvingTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
