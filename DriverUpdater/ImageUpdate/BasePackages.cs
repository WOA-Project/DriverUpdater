using System.Collections.Generic;
using System.Xml.Serialization;

namespace DriverUpdater.ImageUpdate
{
    [XmlRoot(ElementName = "BasePackages", Namespace = "http://schemas.microsoft.com/embedded/2004/10/ImageUpdate")]
    public class BasePackages
    {
        [XmlElement(ElementName = "PackageFile", Namespace = "http://schemas.microsoft.com/embedded/2004/10/ImageUpdate")]
        public List<PackageFile> PackageFile
        {
            get; set;
        }
    }
}
