using NuGet;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace NuSpecHelper
{
    internal class OurOwnNuSpec
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
        
        public OurOwnNuSpec(FileInfo fileinfo)
        {
            SpecFile = fileinfo;
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
                foreach (var dependency in Dependencies)
                {
                    yield return dependency;
                    foreach (var v2 in Repository.GetAllDependecies(dependency.Id))
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
            if (false)
            {
                //using (var r = SpecFile.OpenRead())
                //{
                //    var man = Manifest.ReadFrom(r, false);
                //    var dep = man.Metadata.DependencySets.FirstOrDefault().Dependencies;
                //    foreach (var item in dep)
                //    {
                //        //VersionSpec
                //        //var v = new VersionSpec();
                //        //SemanticVersion.TryParse()
                //    }
                //}
            }  

            _dependencies = new List<PackageIdentity>();
            using (var sr = SpecFile.OpenText())
            {
                var content = sr.ReadToEnd();
                const string pattern = @"<dependency *id=""(?<Name>.*)"" *version=""(?<Version>.*)"" */>";
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

            reporter.AppendLine(@"===" + SpecFile.FullName, Brushes.Blue);
            foreach (var dep in Dependencies)
            {
                reporter.AppendLine(@"  - " + dep.Id + " " + dep.Version);
                var depRequirement = VersionRange.Parse(dep.Version);               
                foreach (var project in Projects)
                {
                    var rp = project.RequiredPackages.FirstOrDefault(p => p.Id == dep.Id);
                    if (rp == null)
                        continue;

                    try
                    {
                        var sv = NuGet.Versioning.NuGetVersion.Parse(rp.Version);
                        var sat = depRequirement.Satisfies(sv);
                        if (!sat)
                        {
                            reporter.AppendLine(
                                $"    - Mismatch: {rp.Version} referenced in {project.ConfigFile.Directory.Name}/{project.ConfigFile.Name}", Brushes.OrangeRed);
                        }
                        else if (string.Compare(rp.Version, depRequirement.MinVersion.ToString(), StringComparison.CurrentCulture) != 0)
                        {
                            reporter.AppendLine(
                                $"    - Warning: MinVersion {depRequirement.MinVersion.ToString()} is lower than installed {rp.Version} in {project.ConfigFile.Directory.Name}/{project.ConfigFile.Name}", Brushes.Orange);
                        }

                    }
                    catch (Exception)
                    {
                        if (string.Compare(rp.Version, dep.Version, StringComparison.CurrentCulture) != 0)
                        {
                            reporter.AppendLine(
                                $"    - Mismatch: {rp.Version} referenced in {project.ConfigFile.Directory.Name}/{project.ConfigFile.Name}", Brushes.OrangeRed);
                        }
                    }
                }
            }
        }

        internal void ReportArrear(List<OurOwnNuSpec> allNuSpecs, IReporter reporter)
        {
            var lst = allNuSpecs.Where(x => x.Identity == null).ToArray();
            foreach (var l in lst)
            {
                Debug.Write("");
            }

            if (!Dependencies.Any())
                return;

            reporter.AppendLine(@"===" + SpecFile.FullName, Brushes.Blue);
            reporter.AppendLine($"[{Identity.Id}, {Identity.Version}]");
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

                Brush repColor = null;
                if (depRes != "<unknown>")
                {
                    var depRequirement = VersionRange.Parse(depReq);
                    var sv = NuGet.Versioning.NuGetVersion.Parse(depRes);

                    if (!depRequirement.Satisfies(sv))
                    {
                        repColor = Brushes.OrangeRed;
                    }
                    if (depRequirement.MinVersion < sv)
                    {
                        repColor = Brushes.Orange;
                    }

                }

                reporter.AppendLine($" - {depName}: req: {depReq} avail: {depRes}", repColor);
            }
        }

        private DirectoryInfo PackagesFolder
        {
            get
            {
                var dir = SpecFile?.Directory?.GetDirectories("packages").FirstOrDefault();
                return dir;
            }
        }

        IEnumerable<PackageIdentity> GetAvailablePackages()
        {
            var re = new Regex(@"\.\d");

            if (PackagesFolder == null)
                yield break;
            foreach (var dir in PackagesFolder.GetDirectories())
            {
                var m = re.Match(dir.Name);
                if (!m.Success)
                    continue;
                var left = dir.Name.Substring(0, m.Index);
                var right = dir.Name.Substring(m.Index + 1);
                yield return new PackageIdentity() {Id = left, Version = right};
            }
        }

        internal IEnumerable<PackageIdentity> OldPackages()
        {
            if (_projects == null)
                Init();
            var ps = GetAvailablePackages().ToList();
            foreach (var project in _projects)
            {
                foreach (var requiredPackage in project.RequiredPackages)
                {
                    ps.RemoveAll(x => x.FullName == requiredPackage.FullName);
                }
            }
            return ps;
        }

        internal void DeleteDownloadedPackage(PackageIdentity oldpackage)
        {
            if (PackagesFolder == null)
                return;
            var dir = PackagesFolder.GetDirectories(oldpackage.FullName).FirstOrDefault();
            if (dir == null)
                return;
            Debug.WriteLine(dir.FullName);

            DeleteDirectory(dir.FullName, true);
            // dir.Delete(true);
        }

        public static void DeleteDirectory(string path)
        {
            DeleteDirectory(path, false);
        }

        public static void DeleteDirectory(string path, bool recursive)
        {
            // Delete all files and sub-folders?
            if (recursive)
            {
                // Yep... Let's do this
                var subfolders = Directory.GetDirectories(path);
                foreach (var s in subfolders)
                {
                    DeleteDirectory(s, recursive);
                }
            }

            // Get all files of the folder
            var files = Directory.GetFiles(path);
            foreach (var f in files)
            {
                // Get the attributes of the file
                var attr = File.GetAttributes(f);

                // Is this file marked as 'read-only'?
                if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    // Yes... Remove the 'read-only' attribute, then
                    File.SetAttributes(f, attr ^ FileAttributes.ReadOnly);
                }

                // Delete the file
                File.Delete(f);
            }

            // When we get here, all the files of the folder were
            // already deleted, so we just delete the empty folder
            Directory.Delete(path);
        }
    }
}
