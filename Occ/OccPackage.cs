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

        public OccLib Lib
        {
            get { return _lib; }
        }
        
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

       

        public string Name { get; set; }
    }
}
