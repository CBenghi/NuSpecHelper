using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NuSpecHelper.Properties;

namespace NuSpecHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
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

        private RichTextBoxReporter _r;

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (SetupNewReport()) 
                return;
            foreach (var nureq in getNuSpecs(new DirectoryInfo(Folder.Text)))
            {
                nureq.Report(_r);    
            }
            _r.AppendLine("Completed.");
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
            Report.Document  = new FlowDocument();
            return false;
        }

        private IEnumerable<NuSpec> getNuSpecs(DirectoryInfo directoryInfo)
        {
            foreach (var fonnd in directoryInfo.GetFiles(@"*.nuspec"))
            {
                yield return new NuSpec(fonnd);
            }
            foreach (var subFound in directoryInfo.EnumerateDirectories().SelectMany(getNuSpecs))
            {
                yield return subFound;
            }
        }

        private void FindUpdate(object sender, RoutedEventArgs e)
        {
            if (SetupNewReport())
                return;
            var allNuSpecs = getNuSpecs(new DirectoryInfo(Folder.Text)).ToList();

            foreach (var nuspec in allNuSpecs)
            {
                nuspec.ReportArrear(allNuSpecs, _r);
            }
            _r.AppendLine("Completed.");
        }

        Dictionary<string, string> _assemblyFrameworks = new Dictionary<string, string>();

        private void ListClr(object sender, RoutedEventArgs e)
        {
            var ff = new FileFinder {Pattern = @"\.dll$"};
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

        private string ClrVNumber(string fileName)
        {
            var asbly = System.Reflection.Assembly.LoadFrom(fileName);
            var version = asbly.ImageRuntimeVersion;

            var list = asbly.GetCustomAttributes(true);
            var a = list.OfType<TargetFrameworkAttribute>().FirstOrDefault();
            if (a != null)
            {
                Console.WriteLine(a.FrameworkName);
                Console.WriteLine(a.FrameworkDisplayName);
                version += " " + a.FrameworkDisplayName;
            }

            return version;
        }

    }
}
