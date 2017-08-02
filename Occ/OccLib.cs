using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NuSpecHelper.Occ
{
    public class OccLib
    {
        private OccSource _source;

        public OccLib(OccSource source)
        {
            _source = source;
        }

        private List<OccPackage> _packages;
        public List<OccPackage> Packages
        {
            get
            {
                if (_packages != null)
                    return _packages;
                _packages = new List<OccPackage>();
                foreach (var packageName in PackagesNames())
                {
                    _packages.Add(GetPackage(packageName));
                }
                return _packages;
            }
            
        }

        public DirectoryInfo GetDir(string name)
        {
            return new DirectoryInfo(
                Path.Combine(_source.OccSourceFolder.FullName, name)
            );
        }

        private IEnumerable<string> PackagesNames()
        {
            var v = GetDir(Name);
            var extName = new FileInfo(Path.Combine(v.FullName, "PACKAGES"));
            if (!extName.Exists)
                yield break;

            using (var read = extName.OpenText())
            {
                string line;
                while ((line = read.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    bool skip = false;
                    //foreach (var ex in except)
                    //{
                    //    var re = new Regex(ex);
                    //    if (re.IsMatch(line))
                    //    {
                    //        skip = true;
                    //        continue;
                    //    }

                    //}
                    if (skip)
                        continue;

                    yield return line;
                }
            }
        }


        private IEnumerable<string> ExternLibNames(IEnumerable<string> except)
        {
            var v = GetDir(Name);
            var extName = new FileInfo(Path.Combine(v.FullName, "EXTERNLIB"));
            if (!extName.Exists)
                yield break;

            using (var read = extName.OpenText())
            {
                string line;
                while ((line = read.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    bool skip = false;
                    foreach (var ex in except)
                    {
                        var re = new Regex(ex);
                        if (re.IsMatch(line))
                        {
                            skip = true;
                            continue;
                        }
                            
                    }
                    if (skip)
                        continue;
                    
                    yield return line;
                }
            }
        }

        private OccPackage GetPackage(string packageName)
        {
            var p = new OccPackage(this) { Name = packageName };
            return p;
        }

        public string Name { get; set; }

        private bool _included = false;
        

        public void Include(IEnumerable<string> except)
        {
            if (_included)
                return;
            _included = true;
            foreach (var extLibName in ExternLibNames(except))
            {
                _source.GetLib(extLibName).Include(except);
            }
        }
    }
}
