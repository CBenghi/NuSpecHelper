using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuSpecHelper
{
    class CsProj
    {
        private FileInfo found;

        public CsProj(FileInfo found)
        {
            this.found = found;

        }

        static IEnumerable<CsProj> GetFromDir(DirectoryInfo dir)
        {
            foreach (var found in dir.GetFiles(@"*.nuspec"))
            {
                yield return new CsProj(found);
            }
            foreach (var subFound in dir.EnumerateDirectories().SelectMany(GetFromDir))
            {
                yield return subFound;
            }
        }

    }
}
