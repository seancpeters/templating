using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public static class TemplateExtensions
    {
        public static IReadOnlyList<string> Languages(this ITemplateInfo template)
        {
            if (template.Tags != null && template.Tags.TryGetValue("language", out ICacheTag languageTag))
            {
                return languageTag.ChoicesAndDescriptions.Keys.ToList();
            }

            return new List<string>();
        }
    }
}
