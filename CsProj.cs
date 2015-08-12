using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuSpecHelper
{
    class CsProj
    {
        private FileInfo _nuspecFile;

        public CsProj(FileInfo nuspecFile)
        {
            _nuspecFile = nuspecFile;
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
