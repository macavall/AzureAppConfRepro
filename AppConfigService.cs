using Castle.Core.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppConfigRefresh
{
    public class AppConfigService : IAppConfigService
    {
        private readonly IConfiguration _configuration;
        private readonly IConfigurationRefresher _refresher;
        public AppConfigService(IConfiguration configuration, IConfigurationRefresher refresher)
        {
            _configuration = configuration;
            _refresher = refresher;
        }
        public async Task RefreshConfigurationAsync(CancellationToken cancellationToken)6 
        {
            await _refresher.TryRefreshAsync(cancellationToken);
        }
        public string GetValue(string key)
        {
            return _configuration[key];
        }
    }
}
