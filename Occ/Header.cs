using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuSpecHelper.Occ
{
    class Header
    {
        internal Header(string fileName)
        {
            _relativeFileName = fileName;
        }

        public string Name
        {
            get
            {
                return _relativeFileName;
            }
        }

        string _relativeFileName;

        public string Folder
        {
            get
            {
                return Path.GetDirectoryName(_relativeFileName);
            }
        }

        public string JustFolderName
        {
            get
            {
                var fi = new FileInfo(_relativeFileName);
                var folder = fi.Directory;
                return folder.Name;
            }
        }
    }
}
