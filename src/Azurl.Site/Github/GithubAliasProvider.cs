using Azurl.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Azurl.GitHub
{
    public class GithubAliasProvider : IAliasProvider
    {
        private readonly ILogger<GithubAliasProvider> logger;
        private readonly GitHubConfiguration settings;
        public GithubAliasProvider(IOptions<GitHubConfiguration> configuration, ILogger<GithubAliasProvider> logger)
        {
            this.settings = configuration.Value;
            this.logger = logger;
        }
        public async Task<Dictionary<string, string>> Load()
        {
            using (var httpClient = new HttpClient())
            {
                var ticks = System.DateTime.UtcNow.Ticks;
                var url = $"https://raw.githubusercontent.com/{settings.Repository}/{settings.Branch}/aliases.json?ts={ticks}";
                logger.LogInformation($"Loading aliases from url '{url}'.");
                var response = await httpClient.GetStringAsync(url);
                logger.LogDebug($"Deserializing json. Received response was : '{response}'");
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
            }
        }
    }
}
