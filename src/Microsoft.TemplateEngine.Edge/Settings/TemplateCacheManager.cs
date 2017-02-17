using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    // Methods for managing the template caches across all locales.
    // Does not store any caches.
    public class TemplateCacheManager
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;

        public TemplateCacheManager(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
        }

        public void DeleteAllLocaleCacheFiles()
        {
            foreach (string locale in _environmentSettings.SettingsLoader.LocalesWithTemplateCacheFiles)
            {
                _environmentSettings.SettingsLoader.DeleteTemplateCacheForLocale(locale);
            }

            // delete the culture neutral cache
            _environmentSettings.SettingsLoader.DeleteTemplateCacheForLocale(null);
        }

        // Writes template caches for each of the following:
        //  - current locale
        //  - cultures for which new langpacks are installed
        //  - other locales with existing caches are regenerated.
        //  - neutral locale
        public void WriteTemplateCaches(ScannedTemplateInfo newTemplateInfo)
        {
            string currentLocale = _environmentSettings.Host.Locale;
            HashSet<string> localesWritten = new HashSet<string>();
            IReadOnlyList<ITemplate> newTemplates = newTemplateInfo.Templates.Values.ToList();

            // If the current locale exists, always write it.
            if (!string.IsNullOrEmpty(currentLocale))
            {
                WriteTemplateCacheForLocale(currentLocale, newTemplates, newTemplateInfo.LocalizationLocatorsForLocale(currentLocale));
                localesWritten.Add(currentLocale);
            }

            // write caches for any locales which had new langpacks installed
            foreach (string langpackLocale in newTemplateInfo.Locales)
            {
                WriteTemplateCacheForLocale(langpackLocale, newTemplates, newTemplateInfo.LocalizationLocatorsForLocale(langpackLocale));
                localesWritten.Add(langpackLocale);
            }

            foreach (string locale in _environmentSettings.SettingsLoader.LocalesWithTemplateCacheFiles)
            {
                if (!localesWritten.Contains(locale))
                {
                    WriteTemplateCacheForLocale(locale, newTemplates, newTemplateInfo.LocalizationLocatorsForLocale(locale));
                    localesWritten.Add(locale);
                }
            }

            // always write the culture neutral cache
            // It must be written last because when a cache for a culture is first created, it's based on the
            // culture neutral cache, plus newly registered templates.
            // If the culture neutral cache is updated before the new cache is first written,
            // the new cache will have duplicate values.
            //
            // being last may not matter anymore due to changes after the comment was written.
            WriteTemplateCacheForLocale(null, newTemplates, new Dictionary<string, ILocalizationLocator>());
        }

        private void WriteTemplateCacheForLocale(string locale, IReadOnlyList<ITemplate> newTemplateList, IReadOnlyDictionary<string, ILocalizationLocator> newLocatorsForLocale)
        {
            List<TemplateInfo> existingTemplatesForLocale;
            IDictionary<string, ILocalizationLocator> existingLocatorsForLocale;
            string cacheVersion;

            if (!_environmentSettings.SettingsLoader.TryReadTemplateCacheFile(locale, out string existingCacheContentForLocale))
            {
                // The cache for this locale didn't exist previously. Start with the neutral locale as if it were the existing (no locales)
                // For First time setup, the read will "fail" and return the default because it's a new file. So ignore the error.
                _environmentSettings.SettingsLoader.TryReadTemplateCacheFile(null, out existingCacheContentForLocale);
            }

            if (string.Equals(existingCacheContentForLocale, TemplateCache.DefaultEmptyCacheFileContent))
            {
                cacheVersion = TemplateCache.CurrentCacheVersion;
                existingTemplatesForLocale = new List<TemplateInfo>();
            }
            else if (!TemplateCache.TryParseCacheFromJObject(JObject.Parse(existingCacheContentForLocale), out existingTemplatesForLocale, out cacheVersion))
            {
                // TODO: make a better exception
                string outputLocale = locale ?? "culture neutral locale";
                throw new Exception($"Template cache for {locale} was read, but couldn't be parsed.");
            }

            existingLocatorsForLocale = new Dictionary<string, ILocalizationLocator>();

            //
            HashSet<string> foundTemplates = new HashSet<string>();
            List<ITemplateInfo> mergedTemplateList = new List<ITemplateInfo>();

            foreach (ITemplate newTemplate in newTemplateList)
            {
                ILocalizationLocator locatorForTemplate = GetPreferredLocatorForTemplate(newTemplate.Identity, existingLocatorsForLocale, newLocatorsForLocale);
                TemplateInfo localizedTemplate = LocalizeTemplate(newTemplate, locatorForTemplate);
                mergedTemplateList.Add(localizedTemplate);
                foundTemplates.Add(newTemplate.Identity);
            }

            foreach (TemplateInfo existingTemplate in existingTemplatesForLocale)
            {
                if (!foundTemplates.Contains(existingTemplate.Identity))
                {
                    ILocalizationLocator locatorForTemplate = GetPreferredLocatorForTemplate(existingTemplate.Identity, existingLocatorsForLocale, newLocatorsForLocale);
                    TemplateInfo localizedTemplate = LocalizeTemplate(existingTemplate, locatorForTemplate);
                    mergedTemplateList.Add(localizedTemplate);
                    foundTemplates.Add(existingTemplate.Identity);
                }
            }

            bool isCurrentLocale = string.IsNullOrEmpty(locale)
                && string.IsNullOrEmpty(_environmentSettings.Host.Locale)
                || (locale == _environmentSettings.Host.Locale);

            TemplateCache cacheToWrite = new TemplateCache(_environmentSettings, mergedTemplateList.Cast<TemplateInfo>().ToList(), cacheVersion);
            JObject serialized = JObject.FromObject(cacheToWrite);
            _environmentSettings.SettingsLoader.WriteTemplateCacheFile(locale, serialized.ToString());
        }

        // find the best locator (if any). New is preferred over old
        private ILocalizationLocator GetPreferredLocatorForTemplate(string identity, IDictionary<string, ILocalizationLocator> existingLocatorsForLocale, IReadOnlyDictionary<string, ILocalizationLocator> newLocatorsForLocale)
        {
            if (!newLocatorsForLocale.TryGetValue(identity, out ILocalizationLocator locatorForTemplate))
            {
                existingLocatorsForLocale.TryGetValue(identity, out locatorForTemplate);
            }

            return locatorForTemplate;
        }

        private TemplateInfo LocalizeTemplate(ITemplateInfo template, ILocalizationLocator localizationInfo)
        {
            TemplateInfo localizedTemplate = new TemplateInfo
            {
                GeneratorId = template.GeneratorId,
                ConfigPlace = template.ConfigPlace,
                ConfigMountPointId = template.ConfigMountPointId,
                Name = localizationInfo?.Name ?? template.Name,
                Tags = LocalizeCacheTags(template, localizationInfo),
                CacheParameters = LocalizeCacheParameters(template, localizationInfo),
                ShortName = template.ShortName,
                Classifications = template.Classifications,
                Author = localizationInfo?.Author ?? template.Author,
                Description = localizationInfo?.Description ?? template.Description,
                GroupIdentity = template.GroupIdentity,
                Identity = template.Identity,
                DefaultName = template.DefaultName,
                LocaleConfigPlace = localizationInfo?.ConfigPlace ?? null,
                LocaleConfigMountPointId = localizationInfo?.MountPointId ?? Guid.Empty,
                HostConfigMountPointId = template.HostConfigMountPointId,
                HostConfigPlace = template.HostConfigPlace
            };

            return localizedTemplate;
        }

        private IReadOnlyDictionary<string, ICacheTag> LocalizeCacheTags(ITemplateInfo template, ILocalizationLocator localizationInfo)
        {
            if (localizationInfo == null || localizationInfo.ParameterSymbols == null)
            {
                return template.Tags;
            }

            IReadOnlyDictionary<string, ICacheTag> templateTags = template.Tags;
            IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> localizedParameterSymbols = localizationInfo.ParameterSymbols;

            Dictionary<string, ICacheTag> localizedCacheTags = new Dictionary<string, ICacheTag>();

            foreach (KeyValuePair<string, ICacheTag> templateTag in templateTags)
            {
                if (localizedParameterSymbols.TryGetValue(templateTag.Key, out IParameterSymbolLocalizationModel localizationForTag))
                {   // there is loc for this symbol
                    Dictionary<string, string> localizedChoicesAndDescriptions = new Dictionary<string, string>();

                    foreach (KeyValuePair<string, string> templateChoice in templateTag.Value.ChoicesAndDescriptions)
                    {
                        if (localizationForTag.ChoicesAndDescriptions.TryGetValue(templateChoice.Key, out string localizedDesc) && !string.IsNullOrWhiteSpace(localizedDesc))
                        {
                            localizedChoicesAndDescriptions.Add(templateChoice.Key, localizedDesc);
                        }
                        else
                        {
                            localizedChoicesAndDescriptions.Add(templateChoice.Key, templateChoice.Value);
                        }
                    }

                    ICacheTag localizedTag = new CacheTag
                    {
                        Description = localizationForTag.Description ?? templateTag.Value.Description,
                        DefaultValue = templateTag.Value.DefaultValue,
                        ChoicesAndDescriptions = localizedChoicesAndDescriptions
                    };

                    localizedCacheTags.Add(templateTag.Key, localizedTag);
                }
                else
                {
                    localizedCacheTags.Add(templateTag.Key, templateTag.Value);
                }
            }

            return localizedCacheTags;
        }

        private IReadOnlyDictionary<string, ICacheParameter> LocalizeCacheParameters(ITemplateInfo template, ILocalizationLocator localizationInfo)
        {
            if (localizationInfo == null || localizationInfo.ParameterSymbols == null)
            {
                return template.CacheParameters;
            }

            IReadOnlyDictionary<string, ICacheParameter> templateCacheParameters = template.CacheParameters;
            IReadOnlyDictionary<string, IParameterSymbolLocalizationModel> localizedParameterSymbols = localizationInfo.ParameterSymbols;


            Dictionary<string, ICacheParameter> localizedCacheParams = new Dictionary<string, ICacheParameter>();

            foreach (KeyValuePair<string, ICacheParameter> templateParam in templateCacheParameters)
            {
                if (localizedParameterSymbols.TryGetValue(templateParam.Key, out IParameterSymbolLocalizationModel localizationForParam))
                {   // there is loc info for this symbol
                    ICacheParameter localizedParam = new CacheParameter
                    {
                        DataType = templateParam.Value.DataType,
                        DefaultValue = templateParam.Value.DefaultValue,
                        Description = localizationForParam.Description ?? templateParam.Value.Description
                    };

                    localizedCacheParams.Add(templateParam.Key, localizedParam);
                }
                else
                {
                    localizedCacheParams.Add(templateParam.Key, templateParam.Value);
                }
            }

            return localizedCacheParams;
        }

        // return dict is: Identity -> locator
        private IDictionary<string, ILocalizationLocator> GetLocalizationsFromTemplates(IList<TemplateInfo> templateList, string locale)
        {
            IDictionary<string, ILocalizationLocator> locatorLookup = new Dictionary<string, ILocalizationLocator>();

            foreach (TemplateInfo template in templateList)
            {
                if (template.LocaleConfigMountPointId == null
                    || template.LocaleConfigMountPointId == Guid.Empty)
                {   // Indicates an unlocalized entry in the locale specific template cache.
                    continue;
                }

                ILocalizationLocator locator = new LocalizationLocator()
                {
                    Locale = locale,
                    MountPointId = template.LocaleConfigMountPointId,
                    ConfigPlace = template.LocaleConfigPlace,
                    Identity = template.Identity,
                    Author = template.Author,
                    Name = template.Name,
                    Description = template.Description
                    // ParameterSymbols are not needed here. If things get refactored too much, they might become needed
                };

                locatorLookup.Add(locator.Identity, locator);
            }

            return locatorLookup;
        }
    }
}
