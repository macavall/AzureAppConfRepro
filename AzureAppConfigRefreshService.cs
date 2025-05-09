using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;

internal sealed class AzureAppConfigRefreshService : BackgroundService
{
    private readonly IEnumerable<IConfigurationRefresher> _refreshers;
    private readonly ILogger<AzureAppConfigRefreshService> _logger;

    public AzureAppConfigRefreshService(
        IConfigurationRefresherProvider refresherProvider,
        ILogger<AzureAppConfigRefreshService> logger)
    {
        _refreshers = refresherProvider.Refreshers;
        _logger = logger;
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

                // Set a delay (e.g., 5 seconds, matching your Program.cs refresh interval)
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("AzureAppConfig has been stopped");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing Azure App Configuration");
            }
        }
    }
}