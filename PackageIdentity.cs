using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NuGet;

namespace NuSpecHelper
{
    public class PackageIdentity
    {
        public string Id;
        public string Version;
        
        public string FullName
        {
            get { return Id + "." + Version; }
        }

        public SemanticVersion GetMinSemantic()
        {
            if (Version.Contains(","))
            {
                var r = new Regex(@"[\[\(](?<start>[0-9\w_\.]+),(?<finish>[0-9\w_\.]+)[\]\)]");
                var m = r.Match(Version);
                if (m.Success)
                {
                    return  new SemanticVersion(m.Groups["start"].Value);
                }
            }
            return new SemanticVersion(Version);
        }
    }
}
