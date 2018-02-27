using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Microsoft.TemplateEngine.Cli.RestoreCataloger
{
    internal static class ProjFileParser
    {
        // return dictionary contains: package name -> package version
        public static IReadOnlyList<Package> ParsePackageAndToolReferences(Stream projFileStream)
        {
            XDocument xmlDoc;
            List<Package> references = new List<Package>();

            try
            {
                xmlDoc = XDocument.Load(projFileStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return references;
            }

            IEnumerable<XElement> packageRefNodes = xmlDoc.Elements("Project")
                                        .Elements("ItemGroup")
                                        .Elements("PackageReference");

            Console.WriteLine("Package Refs:");
            foreach (XElement node in packageRefNodes)
            {
                XAttribute includeAttr = node.Attribute("Include");
                XAttribute versionAttr = node.Attribute("Version");
                references.Add(new Package(includeAttr.Value, versionAttr?.Value));

                Console.WriteLine($"\t{includeAttr.Value} : {versionAttr.Value}");
            }


            IEnumerable<XElement> toolsRefNodes = xmlDoc.Elements("Project")
                                                    .Elements("ItemGroup")
                                                    .Elements("DotNetCliToolReference");

            Console.WriteLine("Tools Refs:");
            foreach (XElement node in toolsRefNodes)
            {
                XAttribute includeAttr = node.Attribute("Include");
                XAttribute versionAttr = node.Attribute("Version");
                references.Add(new Package(includeAttr.Value, versionAttr?.Value));

                Console.WriteLine($"\t{includeAttr.Value} : {versionAttr.Value}");
            }

            Console.WriteLine("-------------");
            Console.WriteLine();

            return references;
        }
    }
}
