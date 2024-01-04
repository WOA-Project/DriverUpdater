using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace DefinitionMigrator
{
    internal class Program
    {
        private static void ConvertV1DefinitionsToV3(string dir)
        {
            IEnumerable<string> definitions = Directory.EnumerateFiles($"{dir}\\definitions", "*.txt", SearchOption.AllDirectories).ToArray();

            foreach (string definition in definitions)
            {
                Logging.Log($"Processing {definition}");
                string additionalFolder = "\\components\\ANYSOC\\Support\\Desktop\\SUPPORT.DESKTOP.POST_UPGRADE_ENABLEMENT";
                IEnumerable<string> folders = File.ReadAllLines(definition).Where(x => !string.IsNullOrEmpty(x)).Select(x => dir + "\\" + x).Union(new string[] { dir + "\\" + additionalFolder });

                IEnumerable<string> boundInfPackages = folders
                    .SelectMany(x => Directory.EnumerateFiles(x, "*.inf", SearchOption.AllDirectories))
                    .Where(x => x.EndsWith(".inf", StringComparison.InvariantCultureIgnoreCase))
                    .Order();

                List<string> xmlLines = [.. boundInfPackages.Select(inf =>
                {
                    string path = Path.GetDirectoryName(inf);
                    string name = Path.GetFileName(inf);

                    string xmlLine = "            <DriverPackageFile Path=\"$(mspackageroot)" + path.Replace(dir, "") + "\" Name=\"" + name + "\" ID=\"" + Path.GetFileNameWithoutExtension(inf) + "\"/>";
                    return xmlLine;
                }).Order()];

                string start = "<FeatureManifest xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns=\"http://schemas.microsoft.com/embedded/2004/10/ImageUpdate\" Revision=\"1\" SchemaVersion=\"1.3\">\r\n    <Drivers>\r\n        <BaseDriverPackages>";
                string end = "        </BaseDriverPackages>\r\n    </Drivers>\r\n</FeatureManifest>";

                xmlLines.Insert(0, start);
                xmlLines.Add(end);

                File.WriteAllLines(definition.Replace(".txt", ".xml"), xmlLines);
                File.Delete(definition);
            }
        }

        private static void ConvertV2DefinitionsToV3(string dir)
        {
            IEnumerable<string> definitions = Directory.EnumerateFiles($"{dir}\\definitions", "*.txt", SearchOption.AllDirectories).ToArray();

            foreach (string definition in definitions)
            {
                Logging.Log($"Processing {definition}");
                DefinitionParser definitionParser = new(definition);
                IEnumerable<string> folders = definitionParser.DriverDirectories.Where(x => !string.IsNullOrEmpty(x)).Select(x => dir + "\\" + x);

                IEnumerable<string> boundInfPackages = folders
                    .SelectMany(x => Directory.EnumerateFiles(x, "*.inf", SearchOption.AllDirectories))
                    .Where(x => x.EndsWith(".inf", StringComparison.InvariantCultureIgnoreCase))
                    .Order();

                List<string> xmlLines = [.. boundInfPackages.Select(inf =>
                {
                    string path = Path.GetDirectoryName(inf);
                    string name = Path.GetFileName(inf);

                    string xmlLine = "            <DriverPackageFile Path=\"$(mspackageroot)" + path.Replace(dir, "") + "\" Name=\"" + name + "\" ID=\"" + Path.GetFileNameWithoutExtension(inf) + "\"/>";
                    return xmlLine;
                }).Order()];

                List<string> deps = GetAppPackages(dir, definitionParser.AppDirectories);
                List<string> depXmlLines = [.. deps.Select(inf =>
                {
                    string path = Path.GetDirectoryName(inf);
                    string name = Path.GetFileName(inf);

                    string xmlLine = "            <PackageFile Path=\"$(mspackageroot)" + path.Replace(dir, "") + "\" Name=\"" + name + "\" ID=\"" + Path.GetFileNameWithoutExtension(inf) + "\"/>";

                    string licenseFile = Path.GetFileNameWithoutExtension(inf) + ".xml";

                    if (File.Exists(path + "\\" + licenseFile))
                    {
                        xmlLine = "            <PackageFile Path=\"$(mspackageroot)" + path.Replace(dir, "") + "\" Name=\"" + name + "\" ID=\"" + Path.GetFileNameWithoutExtension(inf) + "\" LicenseFile=\"" + licenseFile + "\"/>";
                    }

                    return xmlLine;
                }).Order()];

                string start = "<FeatureManifest xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns=\"http://schemas.microsoft.com/embedded/2004/10/ImageUpdate\" Revision=\"1\" SchemaVersion=\"1.3\">\r\n    <Drivers>\r\n        <BaseDriverPackages>";
                string mid = "        </BaseDriverPackages>\r\n    </Drivers>\r\n    <AppX>\r\n        <AppXPackages>";
                string end = "        </AppXPackages>\r\n    </AppX>\r\n</FeatureManifest>";

                xmlLines.Insert(0, start);
                xmlLines.Add(mid);
                xmlLines.AddRange(depXmlLines);
                xmlLines.Add(end);

                File.WriteAllLines(definition.Replace(".txt", ".xml"), xmlLines);
                File.Delete(definition);
            }
        }

        static void Main(string[] args)
        {
            ConvertV2DefinitionsToV3("C:\\Users\\Gus\\Documents\\GitHub\\SurfaceDuo-Drivers");
        }

        private static List<string> GetAppPackages(string DriverRepo, ReadOnlyCollection<string> appPaths)
        {
            List<string> deps = [];

            foreach (string path in appPaths)
            {
                IEnumerable<string> appxs = Directory.EnumerateFiles($"{DriverRepo}\\{path}", "*.appx", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith(".appx", StringComparison.InvariantCultureIgnoreCase));
                IEnumerable<string> msixs = Directory.EnumerateFiles($"{DriverRepo}\\{path}", "*.msix", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith(".msix", StringComparison.InvariantCultureIgnoreCase));
                IEnumerable<string> appxbundles = Directory.EnumerateFiles($"{DriverRepo}\\{path}", "*.appxbundle", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith(".appxbundle", StringComparison.InvariantCultureIgnoreCase));
                IEnumerable<string> msixbundles = Directory.EnumerateFiles($"{DriverRepo}\\{path}", "*.msixbundle", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith(".msixbundle", StringComparison.InvariantCultureIgnoreCase));

                deps.AddRange(appxs);
                deps.AddRange(msixs);
                deps.AddRange(appxbundles);
                deps.AddRange(msixbundles);
            }

            return deps;
        }
    }
}
