using DriverUpdater.ImageUpdate;
using System.IO;
using System.Xml.Serialization;

namespace DriverUpdater
{
    internal class DefinitionParser
    {
        public FeatureManifest FeatureManifest;

        public DefinitionParser(string DefinitionFile)
        {
            using Stream content = File.OpenRead(DefinitionFile);
            FeatureManifest = Program.serializer.Deserialize(content) as FeatureManifest;
        }
    }
}
