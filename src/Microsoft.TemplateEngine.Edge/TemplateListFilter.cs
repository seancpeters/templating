using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Edge
{
    public static class TemplateListFilter
    {
        public static IReadOnlyCollection<IFilteredTemplateInfo> FilterTemplates(IReadOnlyList<TemplateInfo> templateList, bool exactMatchesOnly, params Func<ITemplateInfo, MatchInfo?>[] filters)
        {
            HashSet<IFilteredTemplateInfo> matchingTemplates = new HashSet<IFilteredTemplateInfo>(FilteredTemplateEqualityComparer.Default);

            foreach (ITemplateInfo template in templateList)
            {
                List<MatchInfo> matchInformation = new List<MatchInfo>();

                foreach (Func<ITemplateInfo, MatchInfo?> filter in filters)
                {
                    MatchInfo? result = filter(template);

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
