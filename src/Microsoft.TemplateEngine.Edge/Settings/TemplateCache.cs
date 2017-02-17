using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
#if !NET45
using System.Runtime.Loader;
#endif
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Mount.FileSystem;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class TemplateCache
    {
        public static readonly string DefaultEmptyCacheFileContent = "{}";
        public static readonly string CurrentCacheVersion = "1.0.0.0";

        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly AliasRegistry _aliasRegistry;

        [JsonProperty]
        public string CacheVersion { get; set; }

        [JsonProperty]
        public List<TemplateInfo> TemplateInfo { get; set; }

        public TemplateCache(IEngineEnvironmentSettings environmentSettings, List<TemplateInfo> templates, string cacheVersion = null)
        {
            _environmentSettings = environmentSettings;
            TemplateInfo = templates;
            CacheVersion = cacheVersion ?? CurrentCacheVersion;
        }

        public TemplateCache(IEngineEnvironmentSettings environmentSettings, JObject parsed)
        {
            _environmentSettings = environmentSettings;
            _aliasRegistry = new AliasRegistry(environmentSettings);

            if (TryParseCacheFromJObject(parsed, out List<TemplateInfo> templateInfo, out string cacheVersion))
            {
                TemplateInfo = templateInfo;
                CacheVersion = cacheVersion;
            }
            else
            {
                // TODO: More explicit exception type
                throw new Exception("Input JObject is not a template cache.");
            }
        }

        // Tries to load the cache for the current locale.
        // If it doesn't exist, clone the culture neutral cache and load it.
        public TemplateCache(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _aliasRegistry = new AliasRegistry(environmentSettings);

            LoadCache();
        }

        public void Reload()
        {
            LoadCache();
        }

        private void LoadCache()
        {
            string cacheContent;
            bool firstTime = false;

            if (!_environmentSettings.SettingsLoader.TryReadTemplateCacheFile(_environmentSettings.Host.Locale, out cacheContent))
            {
                if (!_environmentSettings.SettingsLoader.TryReadTemplateCacheFile(null, out cacheContent))
                {
                    // this happens during first-time setup
                    cacheContent = DefaultEmptyCacheFileContent;
                    firstTime = true;
                }

                // current locale cache doesn't exist. Clone the culture neutral cache (just read)
                _environmentSettings.SettingsLoader.WriteTemplateCacheFile(_environmentSettings.Host.Locale, cacheContent);

                // read the cloned cache, for the current culture.
                if (!_environmentSettings.SettingsLoader.TryReadTemplateCacheFile(_environmentSettings.Host.Locale, out cacheContent))
                {
                    // TODO: More explicit exception type
                    throw new Exception("Unable to clone the culture neutral cache.");
                }
            }

            JObject jsonCacheContent = JObject.Parse(cacheContent);

            if (TryParseCacheFromJObject(jsonCacheContent, out List<TemplateInfo> templateInfo, out string cacheVersion))
            {
                TemplateInfo = templateInfo;
                CacheVersion = cacheVersion;
            }
            else if (firstTime)
            {
                TemplateInfo = new List<TemplateInfo>();
                CacheVersion = CurrentCacheVersion;
            }
            else
            {
                // TODO: More explicit exception type
                throw new Exception("Error reading the template cache.");
            }
        }

        public static bool TryParseCacheFromJObject(JObject jsonCacheContent, out List<TemplateInfo> templateInfo, out string cacheVersion)
        {
            templateInfo = new List<TemplateInfo>();
            bool isParsed = true;

            if (jsonCacheContent.TryGetValue(nameof(TemplateInfo), StringComparison.OrdinalIgnoreCase, out JToken templateInfoToken))
            {
                if (templateInfoToken is JArray arr)
                {
                    foreach (JToken entry in arr)
                    {
                        if (entry != null && entry.Type == JTokenType.Object)
                        {
                            templateInfo.Add(new TemplateInfo((JObject)entry));
                        }
                    }
                }
            }
            else
            {
                isParsed = false;
            }

            if (jsonCacheContent.TryGetValue(nameof(CacheVersion), out JToken versionToken))
            {
                cacheVersion = versionToken.ToString();
            }
            else
            {
                cacheVersion = string.Empty;
                isParsed = false;
            }

            return isParsed;
        }

        // Look in the locale-specific template cache file for templates which match the filters.
        public IReadOnlyCollection<IFilteredTemplateInfo> List(bool exactMatchesOnly, params Func<ITemplateInfo, string, MatchInfo?>[] fitlers)
        {
            HashSet<IFilteredTemplateInfo> matchingTemplates = new HashSet<IFilteredTemplateInfo>(FilteredTemplateEqualityComparer.Default);

            foreach (ITemplateInfo template in TemplateInfo)
            {
                string alias = _aliasRegistry.GetAliasForTemplate(template);
                List<MatchInfo> matchInformation = new List<MatchInfo>();

                foreach (Func<ITemplateInfo, string, MatchInfo?> filter in fitlers)
                {
                    MatchInfo? result = filter(template, alias);

                    if (result.HasValue)
                    {
                        matchInformation.Add(result.Value);
                    }
                }

                FilteredTemplateInfo info = new FilteredTemplateInfo(template, matchInformation);
                if (info.IsMatch || (!exactMatchesOnly && info.IsPartialMatch))
                {
                    matchingTemplates.Add(info);
                }
            }

#if !NET45
            return matchingTemplates;
#else
            return matchingTemplates.ToList();
#endif
        }
    }
}
