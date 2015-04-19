using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuSpecHelper
{
    class ProjectPackages
    {
        public FileInfo ConfigFile;

        public ProjectPackages(FileInfo found)
        {
            ConfigFile = found;
        }

        private List<PackageIdentity> _RequiredPackages;

        public List<PackageIdentity> RequiredPackages
        {
            get
            {
                if (_RequiredPackages == null)
                    Init();
                return _RequiredPackages;
            }
        }


        private void Init()
        {
            _RequiredPackages = new List<PackageIdentity>();
            using (var sr = ConfigFile.OpenText())
            {
                var content = sr.ReadToEnd();
                string pattern = @"<package id=""(?<Name>.*)"" version=""(?<Version>.*)"" targetFramework";
                const RegexOptions regexOptions = RegexOptions.None;
                var regex = new Regex(pattern, regexOptions);
                foreach (Match mtch in regex.Matches(content))
                {
                    var p = new PackageIdentity()
                    {
                        Id = mtch.Groups[@"Name"].Value,
                        Version = mtch.Groups[@"Version"].Value
                    };
                    _RequiredPackages.Add(p);
                }
            }
        }



        static internal IEnumerable<ProjectPackages> GetFromDir(DirectoryInfo dir)
        {
            foreach (var found in dir.GetFiles(@"*packages.config"))
            {
                yield return new ProjectPackages(found);
            }
            foreach (var subFound in dir.EnumerateDirectories().SelectMany(GetFromDir))
            {
                yield return subFound;
            }
        }

    }
}
