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

        private IReadOnlyList<IFilteredTemplateInfo> _bestTemplateMatchList;
        private bool _usingContextMatches;

        public bool TryGetCoreMatchedTemplatesWithDisposition(Func<IFilteredTemplateInfo, bool> filter, out IReadOnlyList<IFilteredTemplateInfo> matchingTemplates)
        {
            matchingTemplates = _coreMatchedTemplates.Where(filter).ToList();
            return matchingTemplates.Count != 0;
        }

        public bool TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<IFilteredTemplateInfo> unambiguousTemplateGroup)
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
            if (!_hasUserInputLanguage)
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

        public IReadOnlyList<IFilteredTemplateInfo> BestTemplateMatchList
        {
            get
            {
                if (_bestTemplateMatchList == null)
                {
                    IReadOnlyList<IFilteredTemplateInfo> templateList;

                    Console.Write("*** GetBestTemplateMatchList()... ");
                    if (TryGetUnambiguousTemplateGroupToUse(out templateList))
                    {
                        Console.WriteLine("Unambiguous");
                        _bestTemplateMatchList = templateList;
                    }
                    else if (!string.IsNullOrEmpty(_templateName) && TryGetAllInvokableTemplates(out templateList))
                    {
                        Console.WriteLine("All Invokable");
                        _bestTemplateMatchList = templateList;
                    }
                    else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsMatch, out templateList))
                    {
                        Console.WriteLine("IsMatch");
                        _bestTemplateMatchList = templateList;
                    }
                    else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsMatchExceptContext, out templateList))
                    {
                        Console.WriteLine("IsMatchExceptContext");
                        _bestTemplateMatchList = templateList;
                    }
                    else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsPartialMatch, out templateList))
                    {
                        Console.WriteLine("IsPartialMatch");
                        _bestTemplateMatchList = templateList;
                    }
                    else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsPartialMatchExceptContext, out templateList))
                    {
                        Console.WriteLine("IsPartialMatchExceptContext");
                        _bestTemplateMatchList = templateList;
                    }
                    else
                    {
                        Console.WriteLine("all in context");
                        _bestTemplateMatchList = _allTemplatesInContext.ToList();
                        _usingContextMatches = true;
                    }
                }

                return _bestTemplateMatchList;
            }
        }

        public bool UsingContextMatches
        {
            get
            {
                return _usingContextMatches;
            }
        }
    }
}
