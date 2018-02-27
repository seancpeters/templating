using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Mount.Archive;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.RestoreCataloger
{
    internal class TemplateRestoreSource
    {
        public TemplateRestoreSource(IEngineEnvironmentSettings environment, ITemplateInfo templateInfo, IList<string> nuGetSources)
        {
            _environment = environment;
            _paths = new Paths(_environment);
            _templateInfo = templateInfo;
            _nuGetSources = nuGetSources;
        }

        private readonly IEngineEnvironmentSettings _environment;
        private readonly Paths _paths;
        private readonly ITemplateInfo _templateInfo;
        private readonly IList<string> _nuGetSources;

        private List<IFile> _projFileList;
        public List<IFile> ProjFileList
        {
            get
            {
                if (_projFileList == null)
                {
                    _projFileList = Template.TemplateSourceRoot.EnumerateFiles("*.*proj", SearchOption.AllDirectories).ToList();
                }

                return _projFileList;
            }
        }

        private Dictionary<string, IReadOnlyList<Package>> _projFileToRequestedPackageMap;

        public IReadOnlyDictionary<string, IReadOnlyList<Package>> ProjFileToRequestedPackageMap
        {
            get
            {
                EnsureProjFileParsing();

                return _projFileToRequestedPackageMap;
            }
        }

        private void EnsureProjFileParsing()
        {
            if (_projFileToRequestedPackageMap == null)
            {
                _projFileToRequestedPackageMap = new Dictionary<string, IReadOnlyList<Package>>();

                foreach (IFile projFile in ProjFileList)
                {
                    using (Stream projSourceStream = projFile.OpenRead())
                    {
                        string projFileRelativeToTemplateRoot = projFile.PathRelativeTo(Template.TemplateSourceRoot);
                        _projFileToRequestedPackageMap[projFileRelativeToTemplateRoot] = ProjFileParser.ParsePackageAndToolReferences(projSourceStream);
                    }
                }
            }
        }

        private ITemplate _template;
        public ITemplate Template
        {
            get
            {
                if (_template == null)
                {
                    _template = _environment.SettingsLoader.LoadTemplate(_templateInfo, null);
                }

                return _template;
            }
        }

        private IReadOnlyDictionary<string, IReadOnlyList<PackageRestoreInfo>> _restoredPackagesByProjFile;
        public IReadOnlyDictionary<string, IReadOnlyList<PackageRestoreInfo>> RestoredPackagesByProjFile
        {
            get
            {
                if (_restoredPackagesByProjFile == null)
                {
                    EnsureRestore();
                }

                return _restoredPackagesByProjFile;
            }
        }

        private void EnsureRestore()
        {
            Dictionary<string, IReadOnlyList<PackageRestoreInfo>> restoreResults = new Dictionary<string, IReadOnlyList<PackageRestoreInfo>>();
            IMountPointFactory zipFileMountPointFactory = _environment.SettingsLoader.Components.OfType<IMountPointFactory>()
                                                                                    .FirstOrDefault(f => f.GetType() == typeof(ZipFileMountPointFactory));

            Console.WriteLine($"Cataloging template: {Template.Identity}");
            if (zipFileMountPointFactory == null)
            {
                throw new Exception("Couldnt find the zip file mount point factory.");
            }

            foreach (KeyValuePair<string, IReadOnlyList<Package>> projFileAndPackages in ProjFileToRequestedPackageMap)
            {
                try
                {
                    string projFile = projFileAndPackages.Key;
                    Console.WriteLine($"\tProj file: {projFile}");
                    List<PackageRestoreInfo> restoreResultsForProjFile = new List<PackageRestoreInfo>();
                    IReadOnlyList<Package> packages = projFileAndPackages.Value;
                    IReadOnlyDictionary<string, string> requestedPackageToVersionListMap = CreatePackageToRequestedVersionsMap(packages);
                    Dictionary<string, HashSet<string>> restoredPackageToVersionListMap = new Dictionary<string, HashSet<string>>();

                    string restoreProjDir = _paths.User.ScratchDir;
                    _paths.CreateDirectory(restoreProjDir);

                    string nugetConfigFilePath = Path.Combine(restoreProjDir, "NuGet.config");
                    WriteNugetConfigFile(nugetConfigFilePath);

                    string restoredPackageCatalogDir = Path.Combine(restoreProjDir, "Catalog", Template.Identity);
                    string restoreProjFileName = $"restore_{Template.Identity}.csproj";
                    Installer.RestorePackages(_paths, restoreProjDir, restoreProjFileName, restoredPackageCatalogDir, packages.ToList(), _nuGetSources);

                    // find the restored packages
                    foreach (string packagePath in _paths.EnumerateFiles(restoredPackageCatalogDir, "*.nupkg", SearchOption.AllDirectories))
                    {
                        if (zipFileMountPointFactory.TryMount(_environment, null, packagePath, out IMountPoint mountPoint)
                            && NupkgInfoHelpers.TryGetPackageInfoFromNuspec(mountPoint, out string packageName, out string restoredVersion))
                        {
                            HashSet<string> restoredVersionList;
                            if (!restoredPackageToVersionListMap.TryGetValue(packageName, out restoredVersionList))
                            {
                                restoredVersionList = new HashSet<string>();
                                restoredPackageToVersionListMap[packageName] = restoredVersionList;
                            }
                            restoredVersionList.Add(restoredVersion);
                        }

                        _environment.SettingsLoader.ReleaseMountPoint(mountPoint);
                    }

                    // make PackageRestoreInfo objects for the restored packages
                    foreach (KeyValuePair<string, HashSet<string>> restoredPackageAndVersions in restoredPackageToVersionListMap)
                    {
                        string packageName = restoredPackageAndVersions.Key;
                        HashSet<string> restoredVersions = restoredPackageAndVersions.Value;
                        string restoredVersionString = string.Join(";", restoredVersions);

                        string requestedVersions;
                        if (!requestedPackageToVersionListMap.TryGetValue(packageName, out requestedVersions))
                        {
                            requestedVersions = "Transitive Dependency";
                        }

                        PackageRestoreInfo restoreInfoForPackage = new PackageRestoreInfo(projFile, packageName, requestedVersions, restoredVersionString);
                        restoreResultsForProjFile.Add(restoreInfoForPackage);
                    }

                    // find the packages that failed to restore
                    foreach (KeyValuePair<string, string> unrestoredPackageInfo in requestedPackageToVersionListMap.Where(x => !restoredPackageToVersionListMap.ContainsKey(x.Key)))
                    {
                        PackageRestoreInfo unrestoredInfoForPackage = new PackageRestoreInfo(projFile, unrestoredPackageInfo.Key, unrestoredPackageInfo.Value);
                        restoreResultsForProjFile.Add(unrestoredInfoForPackage);
                    }

                    if (restoreResultsForProjFile.Count == 0)
                    {
                        // add a placeholder for the report, indicating the proj file had nothing to restore.
                        PackageRestoreInfo placeholder = new PackageRestoreInfo(projFile, "* Nothing to restore *", "N/A", "N/A");
                        restoreResultsForProjFile.Add(placeholder);
                    }

                    restoreResults[projFile] = restoreResultsForProjFile;
                }
                finally
                {
                    _paths.DeleteDirectory(_paths.User.ScratchDir);
                }
            }

            if (restoreResults.Count == 0)
            {
                // add a placehoder for the report, indicating that there were no proj files in the template. (non-project templates are like this).
                restoreResults["No proj files in template"] = new List<PackageRestoreInfo>()
                {
                    new PackageRestoreInfo("* No proj files in template *", "N/A", "N/A", "N/A")
                };
            }

            _restoredPackagesByProjFile = restoreResults;
        }

        private static IReadOnlyDictionary<string, string> CreatePackageToRequestedVersionsMap(IReadOnlyList<Package> packageList)
        {
            Dictionary<string, HashSet<string>> packageToVersionListMap = new Dictionary<string, HashSet<string>>();

            foreach (Package packageInfo in packageList)
            {
                HashSet<string> versionList;

                if (!packageToVersionListMap.TryGetValue(packageInfo.Name, out versionList))
                {
                    versionList = new HashSet<string>();
                    packageToVersionListMap[packageInfo.Name] = versionList;
                }

                versionList.Add(packageInfo.Version);
            }

            return packageToVersionListMap.ToDictionary(x => x.Key, x => string.Join(";", x.Value));
        }

        private void WriteNugetConfigFile(string configFilePath)
        {
            const string configFileContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear />
    <add key=""NuGet.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
  <packageRestore>
    <add key=""enabled"" value=""True"" />
    <add key=""automatic"" value=""True"" />
  </packageRestore >
  <fallbackPackageFolders>
    <clear />
  </fallbackPackageFolders>
</configuration>
";
            _paths.WriteAllText(configFilePath, configFileContent);
        }
    }
}
