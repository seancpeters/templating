// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli
{
    public class TemplateListResolutionResult
    {
        public TemplateListResolutionResult(string templateName, string userInputLanguage, IReadOnlyCollection<IFilteredTemplateInfo> coreMatchedTemplates, IReadOnlyCollection<IFilteredTemplateInfo> allTemplatesInContext)
        {
            _templateName = templateName;
            _hasUserInputLanguage = !string.IsNullOrEmpty(userInputLanguage);
            _coreMatchedTemplates = coreMatchedTemplates;
            _allTemplatesInContext = allTemplatesInContext;
            _bestTemplateMatchList = null;
            _usingContextMatches = false;
        }

        private readonly string _templateName;
        private readonly bool _hasUserInputLanguage;

        private readonly IReadOnlyCollection<IFilteredTemplateInfo> _coreMatchedTemplates;
        private readonly IReadOnlyCollection<IFilteredTemplateInfo> _allTemplatesInContext;

        private bool _usingContextMatches;

        public bool TryGetCoreMatchedTemplatesWithDisposition(Func<IFilteredTemplateInfo, bool> filter, out IReadOnlyList<IFilteredTemplateInfo> matchingTemplates)
        {
            matchingTemplates = _coreMatchedTemplates.Where(filter).ToList();
            return matchingTemplates.Count != 0;
        }

        // If a single template group can be resolved, return it.
        // If the user input a language, default language results are not considered.
        // ignoreDefaultLanguageFiltering = true will also cause default language filtering to be ignored.
        public bool TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<IFilteredTemplateInfo> unambiguousTemplateGroup, bool ignoreDefaultLanguageFiltering = false)
        {
            if (_coreMatchedTemplates.Count == 0)
            {
                unambiguousTemplateGroup = null;
                return false;
            }

            if (_coreMatchedTemplates.Count == 1)
            {
                unambiguousTemplateGroup = new List<IFilteredTemplateInfo>(_coreMatchedTemplates);
                return true;
            }

            // maybe: only use default language if we're trying to invoke
            if (!_hasUserInputLanguage && !ignoreDefaultLanguageFiltering)
            {
                // only consider default language match dispositions if the user did not specify a language.
                List<IFilteredTemplateInfo> defaultLanguageMatchedTemplates = _coreMatchedTemplates.Where(x => x.DispositionOfDefaults
                                                                            .Any(y => y.Location == MatchLocation.DefaultLanguage && y.Kind == MatchKind.Exact))
                                                                            .ToList();

                if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(defaultLanguageMatchedTemplates))
                {
                    if (defaultLanguageMatchedTemplates.Any(x => !x.HasParameterMismatch))
                    {
                        unambiguousTemplateGroup = defaultLanguageMatchedTemplates.Where(x => !x.HasParameterMismatch).ToList();
                        return true;
                    }
                    else
                    {
                        unambiguousTemplateGroup = defaultLanguageMatchedTemplates;
                        return true;
                    }
                }
            }

            List<IFilteredTemplateInfo> paramFiltered = _coreMatchedTemplates.Where(x => !x.HasParameterMismatch).ToList();
            if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(paramFiltered))
            {
                unambiguousTemplateGroup = paramFiltered;
                return true;
            }

            if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(_coreMatchedTemplates))
            {
                unambiguousTemplateGroup = new List<IFilteredTemplateInfo>(_coreMatchedTemplates);
                return true;
            }

            unambiguousTemplateGroup = null;
            return false;
        }

        public bool TryGetAllInvokableTemplates(out IReadOnlyList<IFilteredTemplateInfo> invokableTemplates)
        {
            IEnumerable<IFilteredTemplateInfo> invokableMatches = _coreMatchedTemplates.Where(x => x.IsInvokableMatch);

            if (invokableMatches.Any())
            {
                invokableTemplates = invokableMatches.ToList();
                return true;
            }

            invokableTemplates = null;
            return false;
        }

        public bool TryGetSingularInvokableMatch(out IFilteredTemplateInfo template)
        {
            IReadOnlyList<IFilteredTemplateInfo> invokableMatches = _coreMatchedTemplates.Where(x => x.IsInvokableMatch).ToList();
            if (invokableMatches.Count() == 1)
            {
                template = invokableMatches.First();
                return true;
            }

            IFilteredTemplateInfo highestInGroupIfSingleGroup = TemplateListResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(invokableMatches);
            if (highestInGroupIfSingleGroup != null)
            {
                template = highestInGroupIfSingleGroup;
                return true;
            }

            template = null;
            return false;
        }

        private IReadOnlyList<IFilteredTemplateInfo> _bestTemplateMatchList;
        private IReadOnlyList<IFilteredTemplateInfo> _bestTemplateMatchListIgnoringDefaultLanguageFiltering;

        public IReadOnlyList<IFilteredTemplateInfo> GetBestTemplateMatchList(bool ignoreDefaultLanguageFiltering = false)
        {
            if (ignoreDefaultLanguageFiltering)
            {
                if (_bestTemplateMatchList == null)
                {
                    _bestTemplateMatchList = BaseGetBestTemplateMatchList(ignoreDefaultLanguageFiltering);
                }

                return _bestTemplateMatchList;
            }
            else
            {
                if (_bestTemplateMatchListIgnoringDefaultLanguageFiltering == null)
                {
                    _bestTemplateMatchListIgnoringDefaultLanguageFiltering = BaseGetBestTemplateMatchList(ignoreDefaultLanguageFiltering);
                }

                return _bestTemplateMatchListIgnoringDefaultLanguageFiltering;
            }
        }

        // The core matched templates should not need additioanl default language filtering.
        // The default language dispositions are stored in a different place than the other dispositions,
        // and are not considered for most match filtering.
        private IReadOnlyList<IFilteredTemplateInfo> BaseGetBestTemplateMatchList(bool ignoreDefaultLanguageFiltering)
        {
            IReadOnlyList<IFilteredTemplateInfo> templateList;

            if (TryGetUnambiguousTemplateGroupToUse(out templateList, ignoreDefaultLanguageFiltering))
            {
                return templateList;
            }
            else if (!string.IsNullOrEmpty(_templateName) && TryGetAllInvokableTemplates(out templateList))
            {
                return templateList;
            }
            else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsMatch, out templateList))
            {
                return templateList;
            }
            else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsMatchExceptContext, out templateList))
            {
                return templateList;
            }
            else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsPartialMatch, out templateList))
            {
                return templateList;
            }
            else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsPartialMatchExceptContext, out templateList))
            {
                return templateList;
            }
            else
            {
                Console.WriteLine("all in context");
                templateList = _allTemplatesInContext.ToList();
                _usingContextMatches = true;
                return templateList;
            }
        }

        // If BaseGetBestTemplateMatchList returned a list from _allTemplatesInContext, this is true.
        // false otherwise.
        public bool UsingContextMatches
        {
            get
            {
                return _usingContextMatches;
            }
        }
    }
}
