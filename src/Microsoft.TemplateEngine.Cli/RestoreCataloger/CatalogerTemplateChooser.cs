using System.Collections.Generic;
using System.Linq;
using static Microsoft.TemplateEngine.Cli.TemplateListResolutionResult;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.RestoreCataloger
{
    internal static class CatalogerTemplateChooser
    {
        // Based on the command inputs, determines the templates to catalog in a similar way to how templates are chosen for list display.
        // This is somewhat simplified compared to listing, but probably ok.
        // May become more complicated.
        public static IReadOnlyList<ITemplateMatchInfo> DetermineTemplatesToCatalog(TemplateListResolutionResult templateResolutionResult)
        {
            if (templateResolutionResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousTemplateGroup))
            {
                if (templateResolutionResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo singularMatchedTemplate, out SingularInvokableMatchCheckStatus singleMatchStatus))
                {
                    return new List<ITemplateMatchInfo>()
                    {
                        singularMatchedTemplate
                    };
                }

                return unambiguousTemplateGroup;
            }
            else
            {
                IReadOnlyList<ITemplateMatchInfo> bestTemplateMatches = templateResolutionResult.GetBestTemplateMatchList(true);
                if (templateResolutionResult.UsingContextMatches && !string.IsNullOrEmpty(templateResolutionResult.InputTemplateName))
                {
                    return new List<ITemplateMatchInfo>();
                }
                else
                {
                    return bestTemplateMatches;
                }
            }
        }

        public static IReadOnlyList<ITemplateMatchInfo> KludgyButConsistentWithOtherStuff_DetermineTemplatesToCatalog(TemplateListResolutionResult templateResolutionResult)
        {
            // the conditions in the first 2 ifs are from New3Command.EnterTemplateManipulationFlowAsync() (they're in 1 if in the original)
            if (templateResolutionResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousTemplateGroup))
            {
                if (templateResolutionResult.TryGetSingularInvokableMatch(out ITemplateMatchInfo templateToInvoke, out SingularInvokableMatchCheckStatus singleMatchStatus)
                    && !unambiguousTemplateGroup.Any(x => x.HasParameterMismatch())
                    && !unambiguousTemplateGroup.Any(x => x.HasAmbiguousParameterValueMatch()))
                {
                    return new List<ITemplateMatchInfo>()
                    {
                        templateToInvoke
                    };
                }
                // effectively what is done by HelpForTemplateResolution.DisplayHelpForUnambiguousTemplateGroup()
                else if (!HelpForTemplateResolution.AreAllParamsValidForAnyTemplateInList(unambiguousTemplateGroup)
                            && TemplateListResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(unambiguousTemplateGroup) != null)
                {
                    return new List<ITemplateMatchInfo>();
                }
                else
                {
                    return unambiguousTemplateGroup;
                }
            }

            // the following mimics what is done by HelpForTemplateResolution.DisplayHelpForAmbiguousTemplateGroup
            if (templateResolutionResult.AreAllBestMatchesLanguageMismatches())
            {
                return new List<ITemplateMatchInfo>();
            }

            return templateResolutionResult.GetBestTemplateMatchList(true);
        }
    }
}
