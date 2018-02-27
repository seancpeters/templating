using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Utils
{
    public static class NupkgInfoHelpers
    {
        public static bool TryGetPackageInfoFromNuspec(IMountPoint mountPoint, out string packageName, out string version)
        {
            IList<IFile> nuspecFiles = mountPoint.Root.EnumerateFiles("*.nuspec", SearchOption.TopDirectoryOnly).ToList();

            if (nuspecFiles.Count != 1)
            {
                packageName = null;
                version = null;
                return false;
            }

            using (Stream nuspecStream = nuspecFiles[0].OpenRead())
            {
                XDocument content = XDocument.Load(nuspecStream);
                XElement metadata = content.Root.Elements().FirstOrDefault(x => x.Name.LocalName == "metadata");

                if (metadata == null)
                {
                    packageName = null;
                    version = null;
                    return false;
                }

                packageName = metadata.Elements().FirstOrDefault(x => x.Name.LocalName == "id")?.Value;
                version = metadata.Elements().FirstOrDefault(x => x.Name.LocalName == "version")?.Value;

                if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(version))
                {
                    packageName = null;
                    version = null;
                    return false;
                }
            }

            return true;
        }
    }
}
