using System.Threading;
using System.Threading.Tasks;

namespace AppConfigRefresh
{
    public interface IAppConfigService
    {
        public Task RefreshConfigurationAsync(CancellationToken cancellationToken);

        public string GetValue(string key);
    }
}