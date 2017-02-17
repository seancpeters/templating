using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Mount.FileSystem;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class Scanner
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Paths _paths;

        public Scanner(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _paths = new Paths(environmentSettings);
        }

        // Reads and saves all the comonents found in the templateDir.
        // Reads all the templates and langpacks for the current dir. Stores their details in scannedInfo.
        public void Scan(string templateDir, ScannedTemplateInfo scannedInfo)
        {
            if (templateDir[templateDir.Length - 1] == '/' || templateDir[templateDir.Length - 1] == '\\')
            {
                templateDir = templateDir.Substring(0, templateDir.Length - 1);
            }

            string searchTarget = Path.Combine(_environmentSettings.Host.FileSystem.GetCurrentDirectory(), templateDir.Trim());
            List<string> matches = _environmentSettings.Host.FileSystem.EnumerateFileSystemEntries(Path.GetDirectoryName(searchTarget), Path.GetFileName(searchTarget), SearchOption.TopDirectoryOnly).ToList();

            if (matches.Count == 1)
            {
                templateDir = matches[0];
            }
            else
            {
                foreach (string match in matches)
                {
                    Scan(match, scannedInfo);
                }

                return;
            }

            if (_environmentSettings.SettingsLoader.TryGetMountPointFromPlace(searchTarget, out IMountPoint existingMountPoint))
            {
                ScanForComponents(existingMountPoint, templateDir);
                ScanMountPointForTemplatesAndLangpacks(existingMountPoint, scannedInfo);
            }
            else
            {
                foreach (IMountPointFactory factory in _environmentSettings.SettingsLoader.Components.OfType<IMountPointFactory>().ToList())
                {
                    if (factory.TryMount(_environmentSettings, null, templateDir, out IMountPoint mountPoint))
                    {
                        //Force any local package installs into the content directory
                        if (!(mountPoint is FileSystemMountPoint))
                        {
                            string path = Path.Combine(_paths.User.Packages, Path.GetFileName(templateDir));

                            if (!string.Equals(path, templateDir))
                            {
                                _paths.CreateDirectory(_paths.User.Packages);
                                _paths.Copy(templateDir, path);
                                if (factory.TryMount(_environmentSettings, null, path, out IMountPoint mountPoint2))
                                {
                                    mountPoint = mountPoint2;
                                    templateDir = path;
                                }
                            }
                        }

                        // TODO: consider not adding the mount point if there is nothing to install.
                        // It'd require choosing to not write it upstream from here, which might be better anyway.
                        // "nothing to install" could have a couple different meanings:
                        // 1) no templates, and no langpacks were found.
                        // 2) only langpacks were found, but they aren't for any existing templates - but we won't know that at this point.
                        _environmentSettings.SettingsLoader.AddMountPoint(mountPoint);
                        ScanForComponents(mountPoint, templateDir);
                        ScanMountPointForTemplatesAndLangpacks(mountPoint, scannedInfo);
                    }
                }
            }
        }

        private void ScanMountPointForTemplatesAndLangpacks(IMountPoint mountPoint, ScannedTemplateInfo scannedInfo)
        {
            foreach (IGenerator generator in _environmentSettings.SettingsLoader.Components.OfType<IGenerator>())
            {
                IEnumerable<ITemplate> templateList = generator.GetTemplatesAndLangpacksFromDir(mountPoint, out IList<ILocalizationLocator> localizationInfo);

                foreach (ILocalizationLocator locator in localizationInfo)
                {
                    scannedInfo.AddLocalizationLocator(locator);
                }

                foreach (ITemplate template in templateList)
                {
                    scannedInfo.AddTemplate(template);
                }
            }
        }

        // Scans the input mountPoint and directory for components.
        // Uses the SettingsLoader to write the info to the settings file.
        private void ScanForComponents(IMountPoint mountPoint, string templateDir)
        {
            if (mountPoint.Root.EnumerateFiles("*.dll", SearchOption.AllDirectories).Any())
            {
                string diskPath = templateDir;
                if (mountPoint.Info.MountPointFactoryId != FileSystemMountPointFactory.FactoryId)
                {
                    string path = Path.Combine(_paths.User.Content, Path.GetFileName(templateDir));

                    try
                    {
                        mountPoint.Root.CopyTo(path);
                    }
                    catch (IOException)
                    {
                        return;
                    }

                    diskPath = path;
                }

                foreach (KeyValuePair<string, Assembly> asm in AssemblyLoader.LoadAllAssemblies(_paths, out IEnumerable<string> failures))
                {
                    try
                    {
                        foreach (Type type in asm.Value.GetTypes())
                        {
                            _environmentSettings.SettingsLoader.Components.Register(type);
                        }

                        _environmentSettings.SettingsLoader.AddProbingPath(Path.GetDirectoryName(asm.Key));
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
