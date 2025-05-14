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

public static class AzureAppConfigurationRefreshExtensions2
{
    public static IFunctionsWorkerApplicationBuilder UseAzureAppConfiguration2(this IFunctionsWorkerApplicationBuilder builder)
    {
        IEnumerable<IConfigurationRefresher> refreshers = (ServiceProviderServiceExtensions.GetService<IConfigurationRefresherProvider>(builder.Services.BuildServiceProvider()) ?? throw new InvalidOperationException("Unable to find the required services. Please add all the required services by calling 'IServiceCollection.AddAzureAppConfiguration()' in the application startup code.")).Refreshers;
        if (refreshers != null && refreshers.Count() > 0)
        {
            builder.UseMiddleware<AzureAppConfigurationRefreshMiddleware2>();
        }
        return builder;
    }
}

internal class AzureAppConfigurationRefreshMiddleware2 : IFunctionsWorkerMiddleware
{
    private static readonly long MinimumRefreshInterval = TimeSpan.FromSeconds(1.0).Ticks;

    private long _refreshReadyTime = DateTimeOffset.UtcNow.Ticks;

    private IEnumerable<IConfigurationRefresher> Refreshers { get; }

    public AzureAppConfigurationRefreshMiddleware2(IConfigurationRefresherProvider refresherProvider)
    {
        Refreshers = refresherProvider.Refreshers;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        long ticks = DateTimeOffset.UtcNow.Ticks;
        long num = Interlocked.Read(ref _refreshReadyTime);
        if (num <= ticks && Interlocked.CompareExchange(ref _refreshReadyTime, ticks + MinimumRefreshInterval, num) == num)
        {
            using (ExecutionContext.SuppressFlow())
            {
                foreach (IConfigurationRefresher refresher in Refreshers)
                {
                    Task.Run(() => refresher.TryRefreshAsync());
                }
            }
        }
        
        // OLD VERSION
        //await next(context).ConfigureAwait(continueOnCapturedContext: false);

        await next(context); //.ConfigureAwait(true); // continueOnCapturedContext: false);
    }
}

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
