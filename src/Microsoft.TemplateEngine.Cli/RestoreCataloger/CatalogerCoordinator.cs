using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.RestoreCataloger
{
    internal class CatalogCoordinator
    {
        public CatalogCoordinator(IEngineEnvironmentSettings environment, IReadOnlyList<ITemplateInfo> templatesToCatalog)
            :this(environment, templatesToCatalog, new List<string>())
        {
        }

        public CatalogCoordinator(IEngineEnvironmentSettings environment, IReadOnlyList<ITemplateInfo> templatesToCatalog, IList<string> nuGetSources)
        {
            _environment = environment;
            _templatesToCatalog = templatesToCatalog;
            _nuGetSources = nuGetSources;
        }

        private readonly IEngineEnvironmentSettings _environment;
        private readonly IReadOnlyList<ITemplateInfo> _templatesToCatalog;
        private readonly IList<string> _nuGetSources;
        private IReadOnlyDictionary<string, TemplateRestoreSource> _catalog;

        public bool TryWriteCatalogToFile(string fileName)
        {
            try
            {
                CatalogCsvWriter.WriteCatalogAsCsvFile(this, fileName);
                return true;
            }
            catch (Exception ex)
            {
                Reporter.Error.WriteLine(ex.Message.Bold().Red());
                return false;
            }
        }

        public bool TryWriteCatalogToOutput()
        {
            try
            {
                CatalogCsvWriter.WriteCatalogAsCsv(this);
                return true;
            }
            catch (Exception ex)
            {
                Reporter.Error.WriteLine(ex.Message.Bold().Red());
                return false;
            }
        }

        // template identity -> catalog
        public IReadOnlyDictionary<string, TemplateRestoreSource> Catalog
        {
            get
            {
                if (_catalog == null)
                {
                    Dictionary<string, TemplateRestoreSource> restoreCatalog = new Dictionary<string, TemplateRestoreSource>();

                    foreach (ITemplateInfo templateInfo in _templatesToCatalog)
                    {
                        TemplateRestoreSource source = new TemplateRestoreSource(_environment, templateInfo, _nuGetSources);
                        restoreCatalog.Add(templateInfo.Identity, source);
                    }

                    _catalog = restoreCatalog;
                }

                return _catalog;
            }
        }
    }
}
