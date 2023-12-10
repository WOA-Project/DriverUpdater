using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace DriverUpdater
{
    internal class DefinitionParser
    {
        public ReadOnlyCollection<string> DriverDirectories;
        public ReadOnlyCollection<string> AppDirectories;

        public DefinitionParser(string DefinitionFile)
        {
            string content = File.ReadAllText(DefinitionFile);

            IniParser.IniDataParser parser = new();
            parser.Configuration.ThrowExceptionsOnError = false;
            parser.Configuration.DuplicatePropertiesBehaviour = IniParser.Configuration.IniParserConfiguration.EDuplicatePropertiesBehaviour.AllowAndConcatenateValues;
            parser.Configuration.AllowDuplicateSections = true;
            parser.Configuration.AllowKeysWithoutSection = true;
            parser.Configuration.CaseInsensitive = false;
            parser.Configuration.SkipInvalidLines = true;
            IniParser.IniData data = parser.Parse(content);

            DriverDirectories = new ReadOnlyCollection<string>(data.Sections["Drivers"].Select(x => x.Key).ToList());
            AppDirectories = new ReadOnlyCollection<string>(data.Sections["Apps"].Select(x => x.Key).ToList());
        }
    }
}
