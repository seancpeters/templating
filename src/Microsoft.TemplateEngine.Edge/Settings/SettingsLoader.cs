using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class SettingsLoader : ISettingsLoader
    {
        private const int MaxLoadAttempts = 20;
        public static readonly string HostTemplateFileConfigBaseName = ".host.json";

        private SettingsStore _userSettings;
        //private TemplateCache _userTemplateCache;
        private IMountPointManager _mountPointManager;
        private IComponentManager _componentManager;
        private bool _isLoaded;
        private Dictionary<Guid, MountPointInfo> _mountPoints;
        //private bool _templatesLoaded;
        private readonly Paths _paths;
        private readonly IEngineEnvironmentSettings _environmentSettings;

        public SettingsLoader(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _paths = new Paths(environmentSettings);
        }

        public void Save()
        {
            JObject serialized = JObject.FromObject(_userSettings);
            _paths.WriteAllText(_paths.User.SettingsFile, serialized.ToString());
        }

        private void EnsureLoaded()
        {
            if (_isLoaded)
            {
                return;
            }

            string userSettings = null;
            using (Timing.Over("Read settings"))
                for (int i = 0; i < MaxLoadAttempts; ++i)
                {
                    try
                    {
                        userSettings = _paths.ReadAllText(_paths.User.SettingsFile, "{}");
                        break;
                    }
                    catch (IOException)
                    {
                        if (i == MaxLoadAttempts - 1)
                        {
                            throw;
                        }

                        Thread.Sleep(2);
                    }
                }
            JObject parsed;
            using (Timing.Over("Parse settings"))
                parsed = JObject.Parse(userSettings);
            using (Timing.Over("Deserialize user settings"))
                _userSettings = new SettingsStore(parsed);

            using (Timing.Over("Init probing paths"))
                if (_userSettings.ProbingPaths.Count == 0)
                {
                    _userSettings.ProbingPaths.Add(_paths.User.Content);
                }

            using (Timing.Over("Init Component manager"))
                _componentManager = new ComponentManager(this, _userSettings);
            using (Timing.Over("Init Mount Point manager"))
                _mountPointManager = new MountPointManager(_environmentSettings, _componentManager);

            _mountPoints = new Dictionary<Guid, MountPointInfo>();

            using (Timing.Over("Load mount points"))
                foreach (MountPointInfo info in _userSettings.MountPoints)
                {
                    _mountPoints[info.MountPointId] = info;
                }

            //_userTemplateCache = new TemplateCache(_environmentSettings);

            _isLoaded = true;
        }

        public bool TryReadTemplateCacheFile(string locale, out string cacheFileContent)
        {
            string cacheFilename = _paths.User.ExplicitLocaleTemplateCacheFile(locale);

            if (_paths.Exists(cacheFilename))
            {
                cacheFileContent = _paths.ReadAllText(cacheFilename, TemplateCache.DefaultEmptyCacheFileContent);
                return true;
            }

            cacheFileContent = TemplateCache.DefaultEmptyCacheFileContent;
            return false;
        }

        public void WriteTemplateCacheFile(string locale, string content)
        {
            string cacheFilename = _paths.User.ExplicitLocaleTemplateCacheFile(locale);
            _paths.WriteAllText(cacheFilename, content);

            // TODO: decide if there are any cases where this could be incorrect.
            // Doesn't matter right now anyway, can't get the cache exposed in the ISettingsLoader interface without moving more stuff around.
            //
            //if (string.Equals(locale, _environmentSettings.Host.Locale, StringComparison.OrdinalIgnoreCase))
            //{
            //    _userTemplateCache = new TemplateCache(_environmentSettings);
            //}
        }

        public void DeleteTemplateCacheForLocale(string locale)
        {
            _paths.Delete(_paths.User.CultureNeutralTemplateCacheFile);
        }

        public IReadOnlyList<string> LocalesWithTemplateCacheFiles
        {
            get
            {
                List<string> locales = new List<string>();
                string fileSearchPattern = "*." + _paths.User.TemplateCacheFileBaseName;

                foreach (string fullFilename in _paths.EnumerateFiles(_paths.User.BaseDir, fileSearchPattern, SearchOption.TopDirectoryOnly))
                {
                    string filename = Path.GetFileName(fullFilename);
                    string[] fileParts = filename.Split(new char[] { '.' }, 2);
                    string fileLocale = fileParts[0];

                    if (!string.IsNullOrEmpty(fileLocale) && (fileParts[1] == _paths.User.TemplateCacheFileBaseName))
                    {
                        locales.Add(fileLocale);
                    }
                }

                return locales;
            }
        }

        public void Reload()
        {
            _isLoaded = false;
            EnsureLoaded();
        }

        public ITemplate LoadTemplate(ITemplateInfo info)
        {
            IGenerator generator;
            if (!Components.TryGetComponent(info.GeneratorId, out generator))
            {
                return null;
            }

            IMountPoint mountPoint;
            EnsureLoaded();
            if (!_mountPointManager.TryDemandMountPoint(info.ConfigMountPointId, out mountPoint))
            {
                return null;
            }
            IFileSystemInfo config = mountPoint.FileSystemInfo(info.ConfigPlace);

            IFileSystemInfo localeConfig = null;
            if (!string.IsNullOrEmpty(info.LocaleConfigPlace)
                    && info.LocaleConfigMountPointId != null
                    && info.LocaleConfigMountPointId != Guid.Empty)
            {
                IMountPoint localeMountPoint;
                if (!_mountPointManager.TryDemandMountPoint(info.LocaleConfigMountPointId, out localeMountPoint))
                {
                    // TODO: decide if we should proceed without loc info, instead of bailing.
                    return null;
                }

                localeConfig = localeMountPoint.FileSystemInfo(info.LocaleConfigPlace);
            }

            IFile hostTemplateConfigFile = null;
            if (!string.IsNullOrEmpty(_environmentSettings.Host.HostIdentifier))
            {
                hostTemplateConfigFile = HostTemplateConfigFile(config);
            }

            if (hostTemplateConfigFile == null && _environmentSettings.Host.FallbackHostTemplateConfigNames != null)
            {
                foreach (string fallbackName in _environmentSettings.Host.FallbackHostTemplateConfigNames)
                {
                    string hostTemplateFileName = string.Join(string.Empty, fallbackName, HostTemplateFileConfigBaseName);
                    hostTemplateConfigFile = config.Parent.EnumerateFiles(hostTemplateFileName, SearchOption.TopDirectoryOnly).FirstOrDefault();

                    if (hostTemplateConfigFile != null)
                    {
                        break;
                    }
                }
            }

            ITemplate template;
            using (Timing.Over("Template from config"))
                if (generator.TryGetTemplateFromConfigInfo(config, out template, localeConfig, hostTemplateConfigFile))
                {
                    return template;
                }
                else
                {
                    //TODO: Log the failure to read the template info
                }

            return null;
        }

        public IFile HostTemplateConfigFile(IFileSystemInfo config)
        {
            string hostTemplateFileName = string.Join(string.Empty, _environmentSettings.Host.HostIdentifier, HostTemplateFileConfigBaseName);
            return config.Parent.EnumerateFiles(hostTemplateFileName, SearchOption.TopDirectoryOnly).FirstOrDefault();
        }

        public IComponentManager Components
        {
            get
            {
                EnsureLoaded();
                return _componentManager;
            }
        }

        public IEnumerable<MountPointInfo> MountPoints
        {
            get
            {
                EnsureLoaded();
                return _mountPoints.Values;
            }
        }

        public IEngineEnvironmentSettings EnvironmentSettings => _environmentSettings;

        public void AddProbingPath(string probeIn)
        {
            const int maxAttempts = 10;
            int attemptCount = 0;
            bool successfulWrite = false;

            EnsureLoaded();
            while (!successfulWrite && attemptCount++ < maxAttempts)
            {
                if (!_userSettings.ProbingPaths.Add(probeIn))
                {
                    return;
                }

                try
                {
                    Save();
                    successfulWrite = true;
                }
                catch
                {
                    Thread.Sleep(10);
                    Reload();
                }
            }
        }

        public bool TryGetMountPointInfo(Guid mountPointId, out MountPointInfo info)
        {
            EnsureLoaded();
            using(Timing.Over("Mount point lookup"))
            return _mountPoints.TryGetValue(mountPointId, out info);
        }

        public bool TryGetMountPointInfoFromPlace(string mountPointPlace, out MountPointInfo info)
        {
            EnsureLoaded();
            using (Timing.Over("Mount point place lookup"))
                foreach (MountPointInfo mountInfoToCheck in _mountPoints.Values)
                {
                    if (mountPointPlace.Equals(mountInfoToCheck.Place, StringComparison.OrdinalIgnoreCase))
                    {
                        info = mountInfoToCheck;
                        return true;
                    }
                }

            info = null;
            return false;
        }

        public bool TryGetMountPointFromPlace(string mountPointPlace, out IMountPoint mountPoint)
        {
            if (! TryGetMountPointInfoFromPlace(mountPointPlace, out MountPointInfo info))
            {
                mountPoint = null;
                return false;
            }

            return _mountPointManager.TryDemandMountPoint(info.MountPointId, out mountPoint);
        }

        public void AddMountPoint(IMountPoint mountPoint)
        {
            if(_mountPoints.Values.Any(x => string.Equals(x.Place, mountPoint.Info.Place) && x.ParentMountPointId == mountPoint.Info.ParentMountPointId))
            {
                return;
            }

            _mountPoints[mountPoint.Info.MountPointId] = mountPoint.Info;
            _userSettings.MountPoints.Add(mountPoint.Info);
            JObject serialized = JObject.FromObject(_userSettings);
            _paths.WriteAllText(_paths.User.SettingsFile, serialized.ToString());
        }

        public bool TryGetFileFromIdAndPath(Guid mountPointId, string place, out IFile file)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(place) && _mountPointManager.TryDemandMountPoint(mountPointId, out IMountPoint mountPoint))
            {
                file = mountPoint.FileInfo(place);
                return file != null && file.Exists;
            }

            file = null;
            return false;
        }
    }
}
