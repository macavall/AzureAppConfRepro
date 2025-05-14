# App Config Refresh with Azure Durable Functions Work Around (Not confirmed as supported -> This is a test)

[Code where Fixed is in AzureAppConfigurationRefreshMiddleware2.cs](AzureAppConfigurationRefreshMiddleware2.cs#L59)

```csharp
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
```
