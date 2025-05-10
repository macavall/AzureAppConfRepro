using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using Microsoft.FeatureManagement;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Middleware;
using System.Linq;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = FunctionsApplication.CreateBuilder(args);

        builder.Services.AddLogging(logging =>
        {
            logging.AddFilter("Microsoft.Extensions.Configuration.AzureAppConfiguration", LogLevel.Debug);
        });

        // Connect to Azure App Configuration
        builder.Configuration.AddAzureAppConfiguration(options =>
        {
            string connectionString = Environment.GetEnvironmentVariable("AZURE_APPCONFIG_CONNECTION_STRING") ??
                throw new InvalidOperationException("The environment variable 'AZURE_APPCONFIG_CONNECTION_STRING' is not set or is empty.");
            options.Connect(connectionString)
                   // Load all keys that start with `TestApp:` and have no label
                   .Select("TestApp");
        });

        //builder.Services
        //    .AddApplicationInsightsTelemetryWorkerService()
        //    .ConfigureFunctionsApplicationInsights()
        //    .AddAzureAppConfiguration()
        //    .AddFeatureManagement()
        //    .AddSingleton<ConfigurationRefreshLogger>();

        builder.Services.AddApplicationInsightsTelemetryWorkerService();
        builder.Services.ConfigureFunctionsApplicationInsights();
        builder.Services.AddAzureAppConfiguration();
        builder.Services.AddFeatureManagement();

        builder.UseMiddleware<SuppressRefreshMiddleware>(); 
        //builder.Services.AddSingleton<ConfigurationRefreshLogger>();

        builder.UseAzureAppConfiguration();
        builder.ConfigureFunctionsWebApplication();

        builder.Build().Run();
    }
}

internal sealed class SuppressRefreshMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<SuppressRefreshMiddleware> _logger;
    private readonly IConfigurationRefresherProvider _refresherProvider;

    public SuppressRefreshMiddleware(ILogger<SuppressRefreshMiddleware> logger, IConfigurationRefresherProvider refresherProvider)
    {
        _logger = logger;
        _refresherProvider = refresherProvider;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Log any access to refreshers
        if (_refresherProvider.Refreshers.Any())
        {
            _logger.LogWarning("AzureAppConfigurationRefreshMiddleware may attempt refresh. Refreshers detected: {Count}", _refresherProvider.Refreshers.Count());
        }

        // Proceed to the next middleware
        await next(context);

        _logger.LogInformation("SuppressRefreshMiddleware completed for function {FunctionName}", context.FunctionDefinition.Name);
    }
}

//public class ConfigurationRefreshLogger
//{
//    private readonly IConfigurationRefresherProvider _refresherProvider;
//    private readonly ILogger<ConfigurationRefreshLogger> _logger;

//    public ConfigurationRefreshLogger(IConfigurationRefresherProvider refresherProvider, ILogger<ConfigurationRefreshLogger> logger)
//    {
//        _refresherProvider = refresherProvider;
//        _logger = logger;

//        // Log when refreshers are accessed
//        foreach (var refresher in _refresherProvider.Refreshers)
//        {
//            _logger.LogWarning("Configuration refresher detected. Monitoring for refresh attempts.");
//        }
//    }

//    // Method to manually test or monitor refresh (optional)
//    public async Task TryRefreshAsync()
//    {
//        _logger.LogError("Refresh attempt detected in ConfigurationRefreshLogger.TryRefreshAsync");
//        foreach (var refresher in _refresherProvider.Refreshers)
//        {
//            await refresher.TryRefreshAsync();
//            _logger.LogError("Configuration refresh executed for refresher.");
//        }
//    }
//}