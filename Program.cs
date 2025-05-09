using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.FeatureManagement;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using System.Collections.Generic;
using System.Threading;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);

        // Connect to Azure App Configuration
        builder.Configuration.AddAzureAppConfiguration(options =>
        {
            string connectionString = Environment.GetEnvironmentVariable("AZURE_APPCONFIG_CONNECTION_STRING") ??
                throw new InvalidOperationException("The environment variable 'AZURE_APPCONFIG_CONNECTION_STRING' is not set or is empty.");
            options.Connect(connectionString)
                   // Load all keys that start with `TestApp:` and have no label
                   .Select("TestApp");
        });

        builder.Services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()
            .AddHostedService<AzureAppConfigRefreshService>()
            .AddAzureAppConfiguration()
            .AddFeatureManagement();

        builder.UseAzureAppConfiguration();
        builder.ConfigureFunctionsWebApplication();

        //builder
        //.UseWhen<StampHttpHeaderMiddleware>((context) =>
        //{
        //    Console.WriteLine(context.FunctionDefinition.InputBindings.Values.First(a => a.Type.EndsWith("Trigger")).Type);
        //    // We want to use this middleware only for http trigger invocations.
        //    return context.FunctionDefinition.InputBindings.Values
        //                    .First(a => a.Type.EndsWith("Trigger")).Type == "httpTrigger";
        //});

        builder.Build().Run();
    }

    internal sealed class AzureAppConfigRefreshService : BackgroundService
    {
        private readonly IEnumerable<IConfigurationRefresher> _refreshers;

        public AzureAppConfigRefreshService(IConfigurationRefresherProvider refresherProvider)
        {
            _refreshers = refresherProvider.Refreshers;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var refresher in _refreshers)
                    {
                        await refresher.TryRefreshAsync(stoppingToken);
                    }

                    await Task.Delay(0);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    Console.WriteLine("AzureAppConfig has been stopped");
                    break;
                }
            }
        } 
    }
}
