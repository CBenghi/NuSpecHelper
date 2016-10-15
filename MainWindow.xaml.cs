using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using FindConflictingReference;
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

            if (!chkSaveConfig.IsChecked.Value)
                return false;
            Settings.Default.SearchFolder = Folder.Text;
            Settings.Default.Save();
            return false;
        }

        private static IEnumerable<NuSpec> GetNuSpecs(DirectoryInfo directoryInfo)
        {
            foreach (var fonnd in directoryInfo.GetFiles(@"*.nuspec"))
            {
                yield return new NuSpec(fonnd);
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

        private HierarchicalRepo _repos = new HierarchicalRepo();

        private void ListRequired(object sender, RoutedEventArgs e)
        {
            if (SetupNewReport())
                return;
            using (new WaitCursor())
            {
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
    }
}