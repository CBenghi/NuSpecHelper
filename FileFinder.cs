using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuSpecHelper
{
    internal class FileFinder
    {
        private Regex _re = null;
        public string Pattern
        {
            set
            {
                _re = new Regex(value, RegexOptions.IgnoreCase);
            }
        }

       internal IEnumerable<FileInfo> Files(DirectoryInfo d)
       {
           if (_re == null)
               yield break;
           foreach (var fileInfo in d.GetFiles(@"*.dll"))
           {
               Debug.WriteLine(fileInfo.FullName);
               if (_re.IsMatch(fileInfo.FullName))
                   yield return fileInfo;
           }
           foreach (var directoryInfo in d.GetDirectories())
           {
               foreach (var file in Files(directoryInfo))
               {
                   yield return file;
               }
           }
        }
    }
}
