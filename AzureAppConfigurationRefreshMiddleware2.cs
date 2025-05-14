using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppConfigRefresh
{
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
}
