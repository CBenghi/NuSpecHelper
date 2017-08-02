using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuSpecHelper.Occ
{
    public class OccPackage
    {
        private OccLib _lib;

        public string SourceRelativeFolder => $"OCC\\src\\{Name}";

        public OccPackage(OccLib lib)
        {
            _lib = lib;
        }

        public IEnumerable<string> FileNames()
        {
            var v = _lib.GetDir(Name);
            var extName = new FileInfo(Path.Combine(v.FullName, "FILES"));
            if (!extName.Exists)
                yield break;

            using (var read = extName.OpenText())
            {
                string line;
                while ((line = read.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    
                    yield return line;
                }
            }
        }

        public string GetAction(string extension)
        {
            switch (extension)
            {
                case ".hxx":
                case ".h":
                    return "ClInclude";
                case ".cxx":
                case ".c":
                    return "ClCompile";
                case ".rc":
                    return "ResourceCompile";
                case ".ico":
                    return "Image";
                case ".txt":
                    return "Text";
                case ".Configuration":
                case ".Core":
                case ".Data":
                case ".Xml":
                    return "Reference";
                default:
                    return "None";
            }
        }

        public string Name { get; set; }
    }
}
