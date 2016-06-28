using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Azurl.Core
{
    public class AliasRegistry
    {
        private readonly object lockObj = new object();
        private readonly ILogger<AliasRegistry> logger;
        private Dictionary<string, string> aliases;
        private IAliasProvider provider;
        private readonly string cacheFile;

        public  AliasRegistry(IHostingEnvironment env, IAliasProvider provider, ILogger<AliasRegistry> logger)
        {
            this.logger = logger;
            this.cacheFile = Path.Combine(env.ContentRootPath, "cache", "aliases.json");
            this.provider = provider;
            if (File.Exists(cacheFile))
                LoadAliasesFromCache();
            else
                LoadAliasesFromProvider();
            logger.LogDebug("Alias registry initialized.");
        }

        private void LoadAliasesFromCache()
        {
            logger.LogInformation("Loading aliases from cachefile.");
            logger.LogDebug($"Cachefile path is'{this.cacheFile}'");
            lock (lockObj)
            {
                aliases = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(cacheFile, Encoding.UTF8));
            }
        }

        public void LoadAliasesFromProvider()
        {
            lock (lockObj)
            {
                logger.LogInformation("Loading aliases from provider.");
                logger.LogDebug($"Provider type is'{this.provider.GetType().ToString()}'");
                aliases = provider.Load().Result;
                logger.LogDebug("Updating local cache with provided aliases.");
                logger.LogDebug($"Cachefile path is'{this.cacheFile}'");
                Directory.CreateDirectory(Path.GetDirectoryName(cacheFile));
                File.WriteAllText(cacheFile, JsonConvert.SerializeObject(aliases), Encoding.UTF8);
            }
        }

        public string Resolve(string alias)
        {
            if (alias == null)
                throw new ArgumentNullException("alias");

            lock (lockObj)
            {
                if (aliases.ContainsKey(alias))
                    return aliases[alias];
            }
            return "";
        }
    }
}