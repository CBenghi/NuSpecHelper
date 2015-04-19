using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (SetupNewReport()) 
                return;
            foreach (var nureq in getNuSpecs(new DirectoryInfo(Folder.Text)))
            {
                Report.Text += nureq.Report();    
            }
            Report.Text += "Completed.";
        }

        private bool SetupNewReport()
        {
            if (!Directory.Exists(Folder.Text))
            {
                Report.Text = @"Folder not found.";
                return true;
            }
            Settings.Default.SearchFolder = Folder.Text;
            Settings.Default.Save();
            Report.Text = "";
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
                Report.Text += nuspec.ReportArrear(allNuSpecs);        
            }
            Report.Text += "Completed.";
        }
    }
}
