using Ghostwright.Contracts.Inference;
using Ghostwright.Hosting;
using Ghostwright.Platform.Anthropic;
using Ghostwright.Platform.LinkedIn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ghostwright.Sample.Console;

/// <summary>
/// Sample console application demonstrating Ghostwright usage.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            System.Console.WriteLine("Ghostwright Sample Console Application");
            System.Console.WriteLine("======================================");
            System.Console.WriteLine();

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddGhostwright(ghost =>
                    {
                        // Configure the browser kernel
                        ghost.ConfigureKernel(kernel =>
                        {
                            kernel.Headless = true;
                        });

                        // Register platform extensions
                        ghost.UseExtension<AnthropicExtension>();
                        ghost.UseExtension<LinkedInExtension>();
                    });
                })
                .Build();

            System.Console.WriteLine("Ghostwright configured successfully!");
            System.Console.WriteLine();
            System.Console.WriteLine("Registered services:");

            // Demonstrate that services are registered
            var serviceProvider = host.Services;

            var inferenceClient = serviceProvider.GetService<IInferenceClient>();
            System.Console.WriteLine($"  - IInferenceClient: {(inferenceClient != null ? "Registered" : "Not found")}");

            System.Console.WriteLine();
            System.Console.WriteLine("Sample completed. In a real application, you would use these services");
            System.Console.WriteLine("to automate browser interactions with Claude, LinkedIn, and other platforms.");

            await host.StopAsync();
        }
        catch (NotImplementedException ex)
        {
            System.Console.WriteLine("Note: The sample crashed because Patchright is stubbed in this environment. In a real environment with the full Patchright library, this would launch the browser.");
            System.Console.WriteLine($"Exception: {ex.Message}");
        }
    }
}
