using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NuSpecHelper
{
    class NuSpec
    {
        internal FileInfo SpecFile;

        private PackageIdentity _identity;

        internal PackageIdentity Identity
        {
            get
            {
                if (_identity == null)
                {
                    Init();
                }
                return _identity;
            }
            
        }
        
        public NuSpec(FileInfo fonnd)
        {
            SpecFile = fonnd;
        }
        
        private List<PackageIdentity> _dependencies;
        public List<PackageIdentity> Dependencies
        {
            get
            {
                if (_dependencies == null)
                    Init();
                return _dependencies;
            }
        }

        public IEnumerable<PackageIdentity> AllDependencies
        {
            get
            {
                if (_dependencies == null)
                    Init();
                foreach (var VARIABLE in _dependencies)
                {
                    yield return VARIABLE;
                    foreach (var v2 in Repository.GetAllDependecies(VARIABLE.Id))
                    {
                        yield return v2;
                    }
                }
            }
        }

        IEnumerable<string> packageFoldersFor(PackageIdentity defPackage)
        {
            var re = new Regex("^" + defPackage.Id + @"\.([\d\. ]+)(-[\w\d]*)*$" );

            if (SpecFile == null)
                yield break;
            if (SpecFile.DirectoryName == null)
                yield break;
            var d = new DirectoryInfo(Path.Combine(SpecFile.DirectoryName, @"Packages"));
            var found = d.EnumerateDirectories(defPackage.Id + "*");
            foreach (var directoryInfo in found)
            {
                if (re.IsMatch(directoryInfo.Name))
                    yield return directoryInfo.Name;    
            }
            
        }

        private List<ProjectPackages> _projects;

        public List<ProjectPackages> Projects
        {
            get
            {
                if (_projects == null)
                    Init();
                return _projects;
            }  
        }


        private void Init()
        {
            _dependencies = new List<PackageIdentity>();
            using (var sr = SpecFile.OpenText())
            {
                var content = sr.ReadToEnd();
                const string pattern = @"<dependency id=""(?<Name>.*)"" version=""(?<Version>.*)"" />";
                const RegexOptions regexOptions = RegexOptions.None;
                var regex = new Regex(pattern, regexOptions);
                foreach (Match mtch in regex.Matches(content))
                {
                    var p = new PackageIdentity()
                    {
                        Id = mtch.Groups[@"Name"].Value,
                        Version = mtch.Groups[@"Version"].Value
                    };
                    _dependencies.Add(p);
                }
                _identity = new PackageIdentity()
                {
                    Id = getXML(@"id", content),
                    Version = getXML("version", content)
                };
            }
            _projects = ProjectPackages.GetFromDir(SpecFile.Directory).ToList();
        }



        private string getXML(string parameter, string searchXml)
        {
            var pattern = string.Format(@"<{0}>(.+)</{0}>", parameter);
            const RegexOptions regexOptions = RegexOptions.None;
            var regex = new Regex(pattern, regexOptions);
            foreach (Match match in regex.Matches(searchXml))
            {
                return match.Groups[1].Value;
            }
            return @"";
        }


        internal void Report(IReporter reporter)
        {
            if (!Dependencies.Any())
                return;

            reporter.AppendLine(@"===" + SpecFile.FullName);
            foreach (var dep in Dependencies)
            {
                reporter.AppendLine(@"  - " + dep.Id + " " + dep.Version);
                foreach (var project in Projects)
                {
                    var rp = project.RequiredPackages.FirstOrDefault(p => p.Id == dep.Id);
                    if (rp == null)
                        continue;
                    if (rp.Version != dep.Version)
                    {
                        reporter.AppendLine(string.Format("    - Mismatch: {0} referenced in {1}", rp.Version, project.ConfigFile.Directory.Name), Brushes.OrangeRed);
                    }
                }
            }
        }

        internal void ReportArrear(List<NuSpec> allNuSpecs, IReporter reporter)
        {
            var lst = allNuSpecs.Where(x => x.Identity == null).ToArray();
            foreach (var l in lst)
            {
                Debug.Write("");
            }

            if (!Dependencies.Any())
                return;

            reporter.AppendLine(@"===" + SpecFile.FullName);
            reporter.AppendLine(string.Format("[{0}, {1}]", Identity.Id, Identity.Version));
            foreach (var dep in Dependencies)
            {
                var depName = dep.Id;
                var depReq = dep.Version;
                var depRes = "<unknown>";
                var depMatch = allNuSpecs.FirstOrDefault(x => x.Identity != null && x.Identity.Id == depName);
                if (depMatch != null)
                {
                    depRes = depMatch.Identity.Version;
                }
                reporter.AppendLine(string.Format(" - {0}: req: {1} avail: {2}", depName, depReq, depRes));
                if (depRes != "<unknown>" && depReq != depRes)
                {
                    reporter.AppendLine("   => WARNING, check the match.", Brushes.OrangeRed);
                }
            }
        }
    }
}
