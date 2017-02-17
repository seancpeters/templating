using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class ScannedTemplateInfo
    {
        private Dictionary<string, ITemplate> _templates;
        private readonly Dictionary<string, Dictionary<string, ILocalizationLocator>> _localizationLocators;

        public ScannedTemplateInfo()
        {
            _templates = new Dictionary<string, ITemplate>();
            _localizationLocators = new Dictionary<string, Dictionary<string, ILocalizationLocator>>();
        }

        public IReadOnlyDictionary<string, ITemplate> Templates
        {
            get
            {
                return _templates;
            }
        }

        public IReadOnlyList<string> Locales
        {
            get
            {
                return _localizationLocators.Keys.ToList();
            }
        }

        public IReadOnlyDictionary<string, ILocalizationLocator> LocalizationLocatorsForLocale(string locale)
        {
            if (_localizationLocators.TryGetValue(locale, out Dictionary<string, ILocalizationLocator> locatorsForLocale))
            {
                return locatorsForLocale;
            }

            return new Dictionary<string, ILocalizationLocator>();
        }

        // Adds the template to the memory cache, keyed on identity.
        // If the identity is the same as an existing one, it's overwritten.
        // (last in wins)
        public void AddTemplate(ITemplate template)
        {
            _templates[template.Identity] = template;
        }

        public void AddLocalizationLocator(ILocalizationLocator locator)
        {
            if (!_localizationLocators.TryGetValue(locator.Locale, out Dictionary<string, ILocalizationLocator> localeLocators))
            {
                localeLocators = new Dictionary<string, ILocalizationLocator>();
                _localizationLocators.Add(locator.Locale, localeLocators);
            }

            localeLocators[locator.Identity] = locator;
        }
    }
}
