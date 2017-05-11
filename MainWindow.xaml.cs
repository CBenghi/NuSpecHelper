using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using FindConflictingReference;
using Newtonsoft.Json;
using NuGet;
using XbimPlugin.MvdXML.Viewing;
using Settings = NuSpecHelper.Properties.Settings;

namespace NuSpecHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            Folder.Text = Settings.Default.SearchFolder;
            if (Folder.Text == "")
            {
                var d = new DirectoryInfo(".");
                Folder.Text = d.FullName;
            }

            _r = new RichTextBoxReporter(Report);
        }

        private readonly RichTextBoxReporter _r;

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            using (new WaitCursor())
            {
                RemovePackageButton.IsEnabled = false;
                if (SetupNewReport())
                    return;
                foreach (var nureq in GetNuSpecs(new DirectoryInfo(Folder.Text)))
                {
                    nureq.Report(_r);
                    if (nureq.OldPackages().Any())
                        RemovePackageButton.IsEnabled = true;
                }
                _r.AppendLine("Completed.");             
            }
        }

        private bool SetupNewReport()
        {
            if (!Directory.Exists(Folder.Text))
            {
                _r.AppendLine(@"Folder not found.");
                return true;
            }

            Report.Document = new FlowDocument();

            // ReSharper disable once PossibleInvalidOperationException
            if (!chkSaveConfig.IsChecked.Value)
                return false;
            Settings.Default.SearchFolder = Folder.Text;
            Settings.Default.Save();
            return false;
        }

        private static IEnumerable<OurOwnNuSpec> GetNuSpecs(DirectoryInfo directoryInfo)
        {
            foreach (var fonnd in directoryInfo.GetFiles(@"*.nuspec"))
            {
                yield return new OurOwnNuSpec(fonnd);
            }
            foreach (var subFound in directoryInfo.EnumerateDirectories().SelectMany(GetNuSpecs))
            {
                yield return subFound;
            }
        }

        private void FindUpdate(object sender, RoutedEventArgs e)
        {
            using (new WaitCursor())
            {
                if (SetupNewReport())
                    return;
                var allNuSpecs = GetNuSpecs(new DirectoryInfo(Folder.Text)).ToList();

                foreach (var nuspec in allNuSpecs)
                {
                    nuspec.ReportArrear(allNuSpecs, _r);
                }
                _r.AppendLine("Completed.");
            }
        }

        private readonly Dictionary<string, string> _assemblyFrameworks = new Dictionary<string, string>();

        private void ListClr(object sender, RoutedEventArgs e)
        {
            using (new WaitCursor())
            {
                var ff = new FileFinder {Pattern = @"\.(dll|exe)$"};
                foreach (var fl in ff.Files(new DirectoryInfo(Folder.Text)))
                {
                    if (_assemblyFrameworks.ContainsKey(fl.Name))
                    {
                        _r.AppendLine(fl + " (prev loaded):" + _assemblyFrameworks[fl.Name]);
                    }
                    else
                    {
                        var v = ClrVNumber(fl.FullName);
                        _assemblyFrameworks.Add(fl.Name, v);
                        _r.AppendLine(fl + " " + v);
                    }
                }
            }
        }

        private readonly Dictionary<string, string> _xbimVersions = new Dictionary<string, string>();
        private void ListXbimAssemblyVersions(object sender, RoutedEventArgs e)
        {
            using (new WaitCursor())
            {
                var ff = new FileFinder { Pattern = @"\.(dll|exe)$" };
                foreach (var fl in ff.Files(new DirectoryInfo(Folder.Text)))
                {
                    if (_xbimVersions.ContainsKey(fl.Name))
                    {
                        _r.AppendLine(fl + " (prev loaded):" + _xbimVersions[fl.Name]);
                    }
                    else
                    {
                        var v = XbimVNumber(fl.FullName);
                        _xbimVersions.Add(fl.Name, v);
                        _r.AppendLine(fl + " " + v);
                    }
                }
            }
        }

        private static string XbimVNumber(string fileName)
        {
            try
            {
                var asbly = Assembly.LoadFrom(fileName);
                var xa = new XbimAssemblyInfo(asbly);
                var version = string.Format("\t{0}\t{1}\t{2}",
                    xa.AssemblyVersion,
                    xa.FileVersion,
                    xa.CompilationTime
                    );
                return version;
            }
            catch (Exception)
            {
                return "Not a Net Assembly.";
            }
        }

        private static string ClrVNumber(string fileName)
        {
            try
            {
                var asbly = Assembly.LoadFrom(fileName);
                var version = asbly.ImageRuntimeVersion;

                var list = asbly.GetCustomAttributes(true);
                var a = list.OfType<TargetFrameworkAttribute>().FirstOrDefault();
                if (a == null)
                    return version;

                Console.WriteLine(a.FrameworkName);
                Console.WriteLine(a.FrameworkDisplayName);
                version += " " + a.FrameworkDisplayName;
                return version;
            }
            catch (Exception)
            {
                return "Not a Net Assembly.";
            }
        }


        private void FindConflict(object sender, RoutedEventArgs e)
        {
            ConflictFinder.FindConflicts(_r, Folder.Text);
        }

        private void RemoveUnused(object sender, RoutedEventArgs e)
        {
            using (new WaitCursor())
            {
                if (SetupNewReport())
                    return;
                foreach (var nureq in GetNuSpecs(new DirectoryInfo(Folder.Text)))
                {
                    if (nureq.OldPackages().Any())
                    {
                        _r.AppendLine("Cleaning " + nureq.Identity.FullName, Brushes.Blue);
                    }
                    foreach (var oldpackage in nureq.OldPackages())
                    {
                        _r.AppendLine("removing " + oldpackage.FullName);
                        nureq.DeleteDownloadedPackage(oldpackage);
                    }
                }
                _r.AppendLine("Completed.");
                RemovePackageButton.IsEnabled = false;
            }
        }

        private void FindOnlineUpdatables(object sender, RoutedEventArgs e)
        {
            if (SetupNewReport())
                return;
            using (new WaitCursor())
            {
                
                // var packages = repo.Search("BuildingSmart", false).ToList();
                
                var allNuSpecs = GetNuSpecs(new DirectoryInfo(Folder.Text)).ToList();

                foreach (var nuspec in allNuSpecs)
                {
                    const string noBranchName = "NoBranch";
                    var branch = noBranchName;
                    try
                    {
                        var repository = NGit.Api.Git.Open(nuspec.SpecFile.DirectoryName);
                        branch = repository.GetRepository().GetBranch();
                    }
                    catch (Exception exo)
                    {
                        Debug.Print(exo.Message);
                    }

                    // only test if branch is specificed
                    if (branch == noBranchName)
                        continue;

                    var repo =  _repos. GetRepo(branch);

                    // actual test.
                    _r.AppendLine("=== Testing " + nuspec.Identity.FullName, Brushes.Blue);
                    foreach (var dep in nuspec.AllDependencies)
                    {
                        var sv = SemanticVersion.Parse(dep.Version);
                        var depFnd = repo.FindPackage(dep.Id, sv);
                        if (depFnd == null)
                            _r.AppendLine("- Error: " + dep.FullName, Brushes.Red);
                        else
                        {
                            IVersionSpec vsp = new VersionSpec() {
                                MinVersion  =  sv, 
                                IsMinInclusive = false
                                };
                            var verFnd = repo.FindPackage(dep.Id, vsp, false, false);
                            if (verFnd != null)
                                _r.AppendLine("- Update available: " + dep.Id + " (" + dep.Version + " => " + verFnd.Version + ")" , Brushes.OrangeRed);
                            else
                                _r.AppendLine("- Ok: " + dep.FullName, Brushes.Green);
                        }
                    }
                }
                _r.AppendLine("Completed.");
            }
        }

        private readonly HierarchicalRepo _repos = new HierarchicalRepo();

        const string NoBranchName = "NoBranch";

        private void ListRequired(object sender, RoutedEventArgs e)
        {
            if (SetupNewReport())
                return;
            using (new WaitCursor())
            {
                var allNuSpecs = GetNuSpecs(new DirectoryInfo(Folder.Text)).ToList();

                foreach (var nuspec in allNuSpecs)
                {
                    var branch = NoBranchName;
                    try
                    {
                        var repository = NGit.Api.Git.Open(nuspec.SpecFile.DirectoryName);
                        branch = repository.GetRepository().GetBranch();
                    }
                    catch (Exception exo)
                    {
                        Debug.Print(exo.Message);
                    }

                    // only test if branch is specificed
                    if (branch == NoBranchName)
                        continue;

                    var repo = _repos.GetRepo(branch);

                    // actual test.
                    _r.AppendLine("=== Testing " + nuspec.Identity.FullName + " on " + branch, Brushes.Blue);
                    foreach (var dep in nuspec.AllDependencies)
                    {
                        if (!dep.Id.StartsWith("Xbim."))
                            continue;
                        var sv = SemanticVersion.Parse(dep.Version);
                        var depFnd = repo.FindPackage(dep.Id, sv);
                        if (depFnd == null)
                            _r.AppendLine("- Missing: " + dep.FullName, Brushes.Red);
                        else
                        {
                            IVersionSpec vsp = new VersionSpec()
                            {
                                MinVersion = sv,
                                IsMinInclusive = false
                            };
                            var verFnd = repo.FindPackage(dep.Id, vsp, false, false);
                            if (verFnd != null)
                                _r.AppendLine("- Update available: " + dep.Id + " (" + dep.Version + " => " + verFnd.Version + ")", Brushes.OrangeRed);
                            else
                                _r.AppendLine("- Ok: " + dep.FullName, Brushes.Green);
                        }
                    }
                }
                _r.AppendLine("Completed.");
            }
        }

        private void FindMissingOnNuget(object sender, RoutedEventArgs e)
        {
            var nugetRepo = _repos.GetRepo();
            var masterRepo = _repos.GetRepo("master");
            using (new WaitCursor())
            {
                if (SetupNewReport())
                    return;
                foreach (var nureq in GetNuSpecs(new DirectoryInfo(Folder.Text)))
                {
                    _r.AppendLine("=== Testing " + nureq.Identity.Id, Brushes.Blue);
                    var onMaster = masterRepo.FindPackagesById(nureq.Identity.Id);
                    var anyproblems = false;
                    foreach (var masterPackage in onMaster)
                    {
                        if (!masterPackage.IsReleaseVersion())
                        {
                            continue;
                        }
                        var ret = nugetRepo.FindPackage(masterPackage.Id, masterPackage.Version);
                        if (ret == null)
                        {
                            _r.AppendLine($"- {masterPackage.Version} missing ", Brushes.Red);
                            anyproblems = true;
                        }
                        else
                        {
                            _r.AppendLine($"- {masterPackage.Version} ok ", Brushes.Green);
                        }
                    }
                    if (!anyproblems)
                    {
                        // _r.AppendLine("- All Ok: ", Brushes.Green);
                    }
                }
            }
        }

        private void FindProjectDependenciesMissingOnNuget(object sender, RoutedEventArgs e)
        {
            if (SetupNewReport())
                return;
            using (new WaitCursor())
            {
                var nugetRepo = _repos.GetRepo();
                var allNuSpecs = GetNuSpecs(new DirectoryInfo(Folder.Text)).ToList();

                foreach (var nuspec in allNuSpecs)
                {
                    var branch = NoBranchName;
                    try
                    {
                        var repository = NGit.Api.Git.Open(nuspec.SpecFile.DirectoryName);
                        branch = repository.GetRepository().GetBranch();
                    }
                    catch (Exception exo)
                    {
                        Debug.Print(exo.Message);
                    }
                    //// only test if branch is develop
                    //if (branch != "develop")
                    //{
                    //    continue;
                    //}
                    _r.AppendLine($"=== Testing {nuspec.Identity.Id} on branch '{branch}'" , Brushes.Blue);

                    foreach (var masterPackage in nuspec.Dependencies)
                    {
                        var sem = masterPackage.GetMinSemantic();
                        //// only tests for non-special versions
                        //if (!string.IsNullOrEmpty(sem.SpecialVersion))
                        //    continue;
                        var ret = nugetRepo.FindPackage(masterPackage.Id, sem);
                        if (ret == null)
                        {
                            _r.AppendLine($"- {masterPackage.Id}.{masterPackage.Version} missing ", Brushes.Red);
                        }
                        else
                        {
                            _r.AppendLine($"- {masterPackage.Id}.{masterPackage.Version} ok ", Brushes.Green);
                        }
                    }
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (SetupNewReport())
                return;

            var v = SemanticVersion.Parse(TxtVersion.Text);
            Debug.WriteLine(v);

            using (new WaitCursor())
            {
                var nugetRepo = _repos.GetRepo();

                semver.tools.IVersionSpec Iver;
                semver.tools.VersionSpec.TryParseNuGet(TxtVersion.Text, out Iver);

                // nugetRepo.FindPackages("", new VersionSpec. , true, true);


            }
        }

        private string ipCacheFileName
        {
            get
            {
                return Path.Combine(
                    UsageDirectory().FullName,
                    "geoCache.txt"
                );
            }
        }

        private Dictionary<string, StatItem> _geoDictionary;

        private void Usage(object sender, RoutedEventArgs e)
        {
            if (_geoDictionary == null)
                InitGeoDictionary();
            Report.Document = new FlowDocument();
            var d = UsageDirectory();
            if (!d.Exists)
            {
                _r.AppendLine(@"Folder not found.");
                return;
            }
            using (new WaitCursor())
            using (var cwr = File.AppendText(ipCacheFileName))
            using (var w = new WebClient())
            {
                foreach (var fName in d.GetFiles(@"*.log"))
                {
                    _r.AppendLine("=== Reporting " + fName, Brushes.Blue);
                    using (var tr = File.OpenText(fName.FullName))
                    {
                        string line;
                        while ((line = tr.ReadLine()) != null)
                        {
                            var arr = line.Split(new[] {'\t'}, StringSplitOptions.None);
                            if (arr.Length != 3)
                                continue;
                            var ip = arr[1];
                            var logTime = DateTime.ParseExact(arr[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                            if (!_geoDictionary.ContainsKey(ip))
                            {
                                var json_data = string.Empty;
                                // attempt to download JSON data as a string
                                var url = @"http://freegeoip.net/json/" + ip;
                                json_data = w.DownloadString(url);
                                if (string.IsNullOrEmpty(json_data))
                                    continue;
                                cwr.WriteLine(json_data.Replace(Environment.NewLine, ""));
                                var deser = JsonConvert.DeserializeObject<IpGeo>(json_data);
                                var s = new StatItem(deser);
                                _geoDictionary.Add(ip, s);
                            }
                            _geoDictionary[ip].Launches.Add(logTime);
                        }
                        foreach (var stat in _geoDictionary.Values)
                        {
                            _r.AppendLine($"{stat.Ip.ip} {stat.Ip.country_name} {stat.Ip.city}", Brushes.Black);
                            foreach (var statLaunch in stat.Launches)
                            {
                                _r.AppendLine($"\t{statLaunch.ToLongDateString()}");
                            }
                        }
                    }
                }
            }
        }

        private void InitGeoDictionary()
        {
            _geoDictionary = new Dictionary<string, StatItem>();
            using (var tr = File.OpenText(ipCacheFileName))
            {
                string line;
                while ((line = tr.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    var deser = JsonConvert.DeserializeObject<IpGeo>(line);
                    var s = new StatItem(deser);
                    _geoDictionary.Add(deser.ip, s);
                }
            }
        }

        private DirectoryInfo UsageDirectory()
        {
            var dbase = new DirectoryInfo(Folder.Text);
            var fullpath = Path.Combine(dbase.FullName, UsageFolder.Text);

            var d = new DirectoryInfo(fullpath);
            return d;
        }
    }
}