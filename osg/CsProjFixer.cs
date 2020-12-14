using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuSpecHelper.osg
{
    class VcxProjFixer
    {
        private FileInfo projectFile;

        public VcxProjFixer(FileInfo project)
        {
            projectFile = project;
        }

        // string[] Excludes = new[] { "ALL_BUILD.vcxproj", "clobber.vcxproj" };
        string[] Includes = new[] { "osg.vcxproj", };




        public void Fix(DirectoryInfo d)
        {
            // todo: replace d with relative.
            d = CleanDir(d); // this takes care of any last slash

            if (!Includes.Contains(projectFile.Name))
                return;

            var runningDir = projectFile.Directory;
            var fileContent = File.ReadAllText(projectFile.FullName);
            var prefixS = "./";
            var prefixBS = ".\\";

            // we are replacing backwards paths only.
            //
            Debug.WriteLine($"Start === {projectFile.FullName}");

            while (runningDir.FullName.Length >= d.FullName.Length)
            {
                var pathBS = runningDir.FullName;
                var pathS = pathBS.Replace(@"\", "/");
                fileContent = fileContent.Replace(pathBS, prefixBS);
                fileContent = fileContent.Replace(pathS, prefixS); // disabled for cmake parameters
                if (prefixS == "./")
                {
                    prefixS = "";
                    prefixBS = "";
                }
                prefixS += @"../";
                prefixBS += @"..\";
                runningDir = CleanDir(runningDir.Parent);
            }
            NewMethod(fileContent, new Regex(@"(.)(C:\\[\w\-. \\]+)(.)"));
            NewMethod(fileContent, new Regex(@"(.)(C:/[\w\-. /]+)(.)"));
            Debug.WriteLine("Done");
            File.WriteAllText(projectFile.FullName, fileContent);
        }

        private static void NewMethod(string fileContent, Regex r)
        {
            var results = r.Matches(fileContent);
            foreach (Match res in results)
            {
                Debug.WriteLine($"{res.Groups[1]} -> {res.Groups[3]} {res.Groups[2]}");
            }
        }

        private static DirectoryInfo CleanDir(DirectoryInfo runningDir)
        {
            runningDir = new DirectoryInfo(Path.Combine(runningDir.Parent.FullName, runningDir.Name));
            return runningDir;
        }
    }
}
