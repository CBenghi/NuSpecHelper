using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using DotSpatial.Projections;
using FindConflictingReference;
using Newtonsoft.Json;
using NuGet;
using NuSpecHelper.Occ;
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
            var dSub = new DirectoryInfo(Folder.Text);
            var subGeom = dSub.GetDirectories("xbimgeometry*").FirstOrDefault();
            if (subGeom != null)
            {
                XbimGeomFolder.Text = Path.Combine(subGeom.FullName, "Xbim.Geometry.Engine");
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
            if (directoryInfo.FullName.Contains("XbimWebUI"))
                yield break;
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
            MessageBox.Show("Not implemented");
            //if (SetupNewReport())
            //    return;

            //var v = SemanticVersion.Parse(TxtVersion.Text);
            //Debug.WriteLine(v);

            //using (new WaitCursor())
            //{
            //    var nugetRepo = _repos.GetRepo();

            //    semver.tools.IVersionSpec Iver;
            //    semver.tools.VersionSpec.TryParseNuGet(TxtVersion.Text, out Iver);
            //}
        }

        private string IpCacheFileName => Path.Combine(
            UsageDirectory().FullName,
            "geoCache.txt"
        );

        private Dictionary<string, IpGeo> _geoDictionary;

        
        
        private void Usage(object sender, RoutedEventArgs e)
        {
            var rt = ReportType.Text;


            Report.Document = new FlowDocument();
            if (_geoDictionary == null)
                InitGeoDictionary();
            
            var d = UsageDirectory();
            if (!d.Exists)
            {
                _r.AppendLine(@"Folder not found.");
                return;
            }
            using (new WaitCursor())
            using (var cwr = File.AppendText(IpCacheFileName))
            using (var w = new WebClient())
            {
                w.Encoding = System.Text.Encoding.UTF8;
                foreach (var fName in d.GetFiles(@"*.log"))
                {
                    _r.AppendLine("=== Reporting " + fName, Brushes.Blue);
                    using (var tr = File.OpenText(fName.FullName))
                    {
                        // prepare data
                        //
                        var data = new Dictionary<string, List<string>>();

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
                                // attempt to download JSON data as a string
                                var url = @"http://freegeoip.net/json/" + ip;
                                var jsonData = w.DownloadString(url);
                                if (string.IsNullOrEmpty(jsonData))
                                    continue;
                                cwr.WriteLine(ToOneLine(jsonData));
                                var deser = JsonConvert.DeserializeObject<IpGeo>(jsonData);
                                // var s = new StatItem(deser);
                                _geoDictionary.Add(ip, deser);
                            }

                            string groupItem;
                            string instanceValue;
                            if (rt == "IP")
                            {
                                groupItem = ip;
                                instanceValue = $"{logTime.ToShortDateString()} {logTime.ToShortTimeString()}";
                            }
                            else
                            {
                                instanceValue = $"{ip} {_geoDictionary[ip].country_name} {_geoDictionary[ip].city} {logTime.ToShortTimeString()}";
                                groupItem = $"{logTime.ToShortDateString()}";
                            }
                            List<string> i;
                            if (data.TryGetValue(groupItem, out i))
                            {
                                i.Add(instanceValue);
                            }
                            else
                            {
                                data.Add(groupItem, new List<string>() { instanceValue }); 
                            }
                        }

                        // now report
                        //
                        foreach (var stat in data.Keys)
                        {
                            // preparation
                            var header = stat;
                            if (rt == "IP")
                            {
                                var ip = _geoDictionary[stat];
                                header = $"{ip.ip} {ip.country_name} {ip.city}";
                            }

                            // reporting
                            if (ReportCount.IsChecked.HasValue && ReportCount.IsChecked.Value)
                            {
                                // only report count
                                _r.AppendLine($"{header}\t{data[stat].Count}" );
                            }
                            else
                            {
                                // full report
                                _r.AppendLine(header);
                                foreach (var statLaunch in data[stat])
                                {
                                    _r.AppendLine("\t" + statLaunch);
                                }
                            }
                        }
                    }
                }
            }
        }

        private string ToOneLine(string jsonData)
        {
            var ret = jsonData;
            ret = ret.Replace("\r", "");
            ret = ret.Replace("\t", "");
            ret = ret.Replace("\n", "");
            return ret;
        }

        private void InitGeoDictionary()
        {
            _geoDictionary = new Dictionary<string, IpGeo>();
            using (var tr = File.OpenText(IpCacheFileName))
            {
                string line;
                int iLineNumber = 0;
                while ((line = tr.ReadLine()) != null)
                {
                    iLineNumber++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    try
                    {
                        var deser = JsonConvert.DeserializeObject<IpGeo>(line);
                        // var s = new StatItem(deser);
                        _geoDictionary.Add(deser.ip, deser);
                    }
                    catch (Exception e)
                    {
                        _r.AppendLine($"{e.Message} on line: {iLineNumber} : {line}", Brushes.Red);
                    }
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

        private void Map(object sender, RoutedEventArgs e)
        {
            var calibration = false;
            if (_geoDictionary == null)
                InitGeoDictionary();

            FileInfo f = new FileInfo("points.scr");
            using (var fw = f.CreateText())
            {
                if (calibration)
                {
                    for (double x = 0; x < 180; x += 30)
                    {
                        for (double y = 0; y < 61; y += 30)
                        {
                            var coord = new[] { x, y };
                            Project(coord, fw);
                        }
                    }
                    return;
                }
                foreach (var statItem in _geoDictionary.Values)
                {
                    var lat = statItem.getLatitude();
                    var lon = statItem.getLongitude();
                    var coord = new[] { lon, lat };
                    Project(coord, fw);
                }
            }
        }

        private void Project(double[] xy, StreamWriter fw)
        {
            //An array for the z coordinate
            double[] z = new double[1];
            z[0] = 1;
            //Defines the starting coordiante system
            var pStart = KnownCoordinateSystems.Geographic.World.WGS1984;
            //Defines the ending coordiante system
            var pEnd = KnownCoordinateSystems.Projected.World.Robinsonworld;
            //Calls the reproject function that will transform the input location to the output locaiton
            Reproject.ReprojectPoints(xy, z, pStart, pEnd, 0, 1);

            fw.WriteLine("point");
            fw.WriteLine($"{xy[0]},{xy[1]}");
        }

        private void FixCFilter(object sender, RoutedEventArgs e)
        {
            return;
            
            var xbim4 = new FileInfo(
                "C:\\Users\\Claudio\\Dev\\XbimTeam\\XbimGeometry\\Xbim.Geometry.Engine\\Xbim.Geometry.Engine.vcxproj.filters");
            var xbim3 = new FileInfo(
                "C:\\Users\\Claudio\\Dev\\Xbim3\\XbimGeometry\\Xbim.Geometry.Engine\\Xbim.Geometry.Engine.vcxproj.filters");
            var xbim3new = new FileInfo(
                "C:\\Users\\Claudio\\Dev\\Xbim3\\XbimGeometry\\Xbim.Geometry.Engine\\Xbim.Geometry.Engine.vcxproj.newfilters");


            string proj4;
            using (var read = xbim4.OpenText())
            {
                proj4 = read.ReadToEnd();
            }

            var findFile = "<Action +Include=\"filename\">[\\s\\n\\r]*<Filter>([\\\\\\w\\s]+)</Filter>";
            


            var re = new Regex("<(?<action>[\\S]+) *Include=\"(?<file>[^\"]+)\" */>");
            using (var write = xbim3new.CreateText())
            using (var read = xbim3.OpenText())
            {
                string line;
                while ((line = read.ReadLine()) != null)
                {
                    var t = re.Match(line);
                    if (t.Success)
                    {
                        var file = t.Groups["file"].Value.Replace("\\", "\\\\");

                        if (file.StartsWith("Xbim"))
                        {
                            write.WriteLine(line);
                            continue;
                        }

                        var action = t.Groups["action"].Value;
                        var findThisFile = findFile
                            .Replace("Action", action)
                            .Replace("filename", file);
                        var thisre = new Regex(findThisFile);

                        var thism = thisre.Match(proj4);
                        if (thism.Success)
                        {
                            var fname = t.Groups["file"].Value;
                            var dest = thism.Groups[1].Value;
                            write.WriteLine($"<{action} Include=\"{fname}\"><Filter>{dest}</Filter></{action}>");
                        }
                        else
                        {
                            write.WriteLine(line);
                        }
                    }
                    else
                    {
                        write.WriteLine(line);
                    }
                }
            }
        }

        private void ListDependencies(object sender, RoutedEventArgs e)
        {
            var occ = GetOccConfig();

            var lstExtensions = new List<string>();
            foreach (var lib in occ.AllLibs())
            {
                // Debug.WriteLine(lib.Name);
                foreach (var libPackage in lib.Packages)
                {
                    // Debug.WriteLine("\t" + libPackage.Name);
                    foreach (var fileName in libPackage.FileNames())
                    {
                        // Debug.WriteLine("\t\t" + fileName);
                        var ext = Path.GetExtension(fileName);

                        if (ext == ".yacc")
                        {
                            Debug.WriteLine("File : " + fileName);
                        }

                        if (lstExtensions.Contains(ext))
                            continue;
                        Debug.WriteLine("Ext: " + ext);
                        lstExtensions.Add(ext);
                    }
                }
            }
            _r.AppendLine("See debug info for this event.");
        }

        private OccSource GetOccConfig()
        {
            var occ = new OccSource(
                OccFolder.Text,
                XbimGeomFolder.Text
                );
            occ.SetDefaultInitialisation();
            return occ;
        }
        
        private void MakeProject(object sender, RoutedEventArgs e)
        {
            var occ = GetOccConfig();
            if (chkCsProj.IsChecked.Value)
                occ.MakeProject();
            if (chkCsProjFilter.IsChecked.Value)
                occ.MakeProjectFilters();
            _r.AppendLine($"Projects created with '.new' extension. {DateTime.Now}");
        }

        private void MakeProjectFilters(object sender, RoutedEventArgs e)
        {
            var occ = GetOccConfig();
            occ.RenameNew(chkCsProj.IsChecked.Value, chkCsProjFilter.IsChecked.Value);
            _r.AppendLine($"Project files renamed. {DateTime.Now}");
        }

        private void ReplaceOccSource(object sender, RoutedEventArgs e)
        {
            var occ = GetOccConfig();
            occ.ReplaceSource(_r);
        }

        private void Geo(object sender, RoutedEventArgs e)
        {
            if (_geoDictionary == null)
                InitGeoDictionary();

            var d = UsageDirectory();

            // counting
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (var fName in d.GetFiles(@"*.log"))
            {
                _r.AppendLine("=== Reporting " + fName, Brushes.Blue);
                using (var tr = File.OpenText(fName.FullName))
                {
                    // prepare data
                    //
                    var data = new Dictionary<string, List<string>>();

                    string line;
                    while ((line = tr.ReadLine()) != null)
                    {
                        var arr = line.Split(new[] { '\t' }, StringSplitOptions.None);
                        if (arr.Length != 3)
                            continue;
                        var ip = arr[1];

                        if (counts.ContainsKey(ip))
                        {
                            counts[ip] += 1;
                        }
                        else
                            counts.Add(ip, 1);
                    }
                }
            }
            Regex r = new Regex("(\\w+)/(\\w+)");
            FileInfo f = new FileInfo("georeport.csv");
            using (var fw = f.CreateText())
            {
                foreach (var key in counts.Keys)
                {
                    var geo = _geoDictionary[key];
                    var timezone = geo.time_zone;

                    var m = r.Match(timezone);
                    var continent = m.Groups[1].Value;
                    if (string.IsNullOrEmpty(continent))
                        continue;

                    fw.WriteLine($"{continent}\t{geo.country_name}\t{geo.region_name}\t{geo.city}\t{counts[key]}");
                }
            }
        }
    }
}