using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
