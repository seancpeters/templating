using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ISettingsLoader
    {
        IComponentManager Components { get; }
   
        IEngineEnvironmentSettings EnvironmentSettings { get; }

        IEnumerable<MountPointInfo> MountPoints { get; }

        void AddMountPoint(IMountPoint mountPoint);

        void AddProbingPath(string probeIn);

        ITemplate LoadTemplate(ITemplateInfo info);

        void Save();

        bool TryGetFileFromIdAndPath(Guid mountPointId, string place, out IFile file);

        bool TryGetMountPointFromPlace(string mountPointPlace, out IMountPoint mountPoint);

        bool TryGetMountPointInfo(Guid mountPointId, out MountPointInfo info);

        bool TryReadTemplateCacheFile(string locale, out string cacheFileContent);

        void WriteTemplateCacheFile(string locale, string content);

        void DeleteTemplateCacheForLocale(string locale);

        IReadOnlyList<string> LocalesWithTemplateCacheFiles { get; }

        IFile HostTemplateConfigFile(IFileSystemInfo config);
    }

    public interface IEngineEnvironmentSettings
    {
        ISettingsLoader SettingsLoader { get; }

        ITemplateEngineHost Host { get; }

        IEnvironment Environment { get; }

        IPathInfo Paths { get; }
    }
}
