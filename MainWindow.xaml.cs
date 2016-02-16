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
            Settings.Default.SearchFolder = Folder.Text;
            Settings.Default.Save();
            Report.Document = new FlowDocument();
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

        private void TestNugetOrg(object sender, RoutedEventArgs e)
        {
            using (new WaitCursor())
            {
                var repo = PackageRepositoryFactory.Default.CreateRepository("https://packages.nuget.org/api/v2");
                // var packages = repo.Search("BuildingSmart", false).ToList();
                if (SetupNewReport())
                    return;
                var allNuSpecs = GetNuSpecs(new DirectoryInfo(Folder.Text)).ToList();

                foreach (var nuspec in allNuSpecs)
                {
                    // only test master
                    var branch = "NoBranch";
                    try
                    {
                        var repository = NGit.Api.Git.Open(nuspec.SpecFile.DirectoryName);
                        branch = repository.GetRepository().GetBranch();
                    }
                    catch (Exception exo)
                    {
                        Debug.Print(exo.Message);
                    }

                    
                    if (branch != "master")
                        continue;

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
    }
}