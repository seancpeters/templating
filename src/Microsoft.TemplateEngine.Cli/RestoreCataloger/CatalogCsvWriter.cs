using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.RestoreCataloger
{
    internal static class CatalogCsvWriter
    {
        public static void WriteCatalogAsCsv(CatalogCoordinator catalog)
        {
            Reporter.Output.WriteLine(GenerateCatalogCsvReport(catalog));
        }

        public static void WriteCatalogAsCsvFile(CatalogCoordinator catalog, string outfilePath)
        {
            File.WriteAllText(outfilePath, GenerateCatalogCsvReport(catalog));
        }

        public static string GenerateCatalogCsvReport(CatalogCoordinator catalog)
        {
            StringBuilder csvTextBuilder = new StringBuilder();
            csvTextBuilder.AppendLine("Template,Identity,Source Proj File,Package Name,Requested Version(s),Restored Version(s)");

            foreach (KeyValuePair<string, TemplateRestoreSource> templateRestoreInfo in catalog.Catalog)
            {
                string templateIdentity = templateRestoreInfo.Key;
                TemplateRestoreSource source = templateRestoreInfo.Value;

                string templateFriendlyName;
                if (source.Template.Languages().Count > 0)
                {
                    templateFriendlyName = $"{source.Template.Name} [{string.Join(", ", source.Template.Languages())}]";
                }
                else
                {
                    templateFriendlyName = source.Template.Name;
                }

                foreach (KeyValuePair<string, IReadOnlyList<PackageRestoreInfo>> projFileRestoreInfo in source.RestoredPackagesByProjFile)
                {
                    string projFile = projFileRestoreInfo.Key;
                    IReadOnlyList<PackageRestoreInfo> packageList = projFileRestoreInfo.Value;

                    foreach (PackageRestoreInfo packageInfo in packageList)
                    {
                        csvTextBuilder.AppendLine($"{templateFriendlyName},{templateIdentity},{projFile},{packageInfo.PackageName},{packageInfo.RequestedVersion},{packageInfo.RestoredVersion}");
                    }
                }
            }

            return csvTextBuilder.ToString();
        }
    }
}
