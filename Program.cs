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
using AppConfigRefresh;

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
                   .Select("TestApp")
                   // Reload configuration if any selected key-values have changed.
                   // Use the default refresh interval of 30 seconds. It can be overridden via AzureAppConfigurationRefreshOptions.SetRefreshInterval.
                   .ConfigureRefresh(refreshOptions =>
                   {
                       refreshOptions.RegisterAll();
                       refreshOptions.SetRefreshInterval(TimeSpan.FromSeconds(5));
                   });
        });

        builder.Services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()
            .AddHostedService<AzureAppConfigRefreshService>()
            .AddAzureAppConfiguration()
            .AddFeatureManagement();

        builder.UseAzureAppConfiguration2();
        builder.ConfigureFunctionsWebApplication();

        builder.Build().Run();
    }
}
