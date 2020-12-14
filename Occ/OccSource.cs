using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuSpecHelper.Occ
{
    public class OccSource
    {
        bool _useTargetFile = false;
       
        public DirectoryInfo GetDir(string name)
        {
            try
            {
                return new DirectoryInfo(
                    Path.Combine(OccSourceFolder.FullName, name)
                );
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public static string GetAction(string extension)
        {
            extension = Path.GetExtension(extension);
            switch (extension)
            {
                case ".hxx":
                case ".pxx":
                case ".h":
                    return "ClInclude";
                case ".cxx":
                case ".c":
                case ".cpp":
                    return "ClCompile";
                case ".rc":
                    return "ResourceCompile";
                case ".ico":
                    return "Image";
                case ".txt":
                    return "Text";
                case ".Configuration":
                case ".Core":
                case ".Data":
                case ".Xml":
                    return "Reference";
                default:
                    return "None";
            }
        }

        public DirectoryInfo OccSourceFolder;
        public DirectoryInfo OccDestFolder;
        public DirectoryInfo GeometryProjectFolder;

        private readonly Dictionary<string, OccLib> _libs = new Dictionary<string, OccLib>();

        public OccLib GetLib(string libName)
        {
            if (_libs.ContainsKey(libName))
                return _libs[libName];
            var retLib = new OccLib(this) { Name = libName }; // "this" sets the source of the lib
            _libs.Add(libName, retLib);
            return retLib;
        }

        List<OccPackage> _extraPackages = new List<OccPackage>();
        List<Header> _extraHeaders = new List<Header>();
        
        public OccSource(string occSourceFolder, string xbimGeometryProjectFolder)
        {
            OccSourceFolder = new DirectoryInfo(occSourceFolder);
            GeometryProjectFolder = new DirectoryInfo(xbimGeometryProjectFolder);
            OccDestFolder = new DirectoryInfo(Path.Combine(
                xbimGeometryProjectFolder,
                "OCC\\src\\"));

            if (GeometryProjectFolder.FullName.ToLowerInvariant().Contains("xbim50"))
                _useTargetFile = true;
        }
        
        public IEnumerable<OccLib> AllLibs()
        {
            return _libs.Values;
        }

        public IEnumerable<OccPackage> AllPackages()
        {
            return AllLibs().SelectMany(lib => lib.Packages);
        }

        FileInfo csProj
        {
            get
            {
                if (_useTargetFile)
                {
                    return new FileInfo(Path.Combine(GeometryProjectFolder.FullName, "Xbim.Geometry.Engine - OCC.targets"));
                }
                else
                {
                    return new FileInfo(Path.Combine(GeometryProjectFolder.FullName, "Xbim.Geometry.Engine.vcxproj"));
                }
            }
        }

        FileInfo csProjNew
        {
            get
            {
                return new FileInfo(csProj.FullName + ".new");
            }
        }

        IEnumerable<string> packageFolders
        {
            get
            {
                List<string> tmp = new List<string>();
                tmp.AddRange(
                    _extraHeaders.Select(x => x.Folder)
                    );
                tmp.AddRange(
                    AllPackages().Select(x => x.SourceRelativeFolder).ToList()
                    );
                return tmp;
            }
        }
            


        public bool MakeProject()
        {
            bool sourcetagfound = false;
            var packageFoldersList = string.Join(";", packageFolders.ToArray());
            var replaceAdditional = $"      <AdditionalIncludeDirectories>{packageFoldersList};$(CSF_OPT_INC);%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>";
            var reIncludeAction = new Regex("<(?<action>[\\S]+) *Include=\"(?<file>[^\"]+?)(\\.(?<ext>[\\w]+))?\" */>", RegexOptions.Compiled);
            var itemGroup = new Regex(" *<ItemGroup *[^>]*>");

            using (var newProject = csProjNew.CreateText())
            using (var read = csProj.OpenText())
            {
                string conditionalBuffer = "";
                string line;
                while ((line = read.ReadLine()) != null)
                {
                    // manage conditional buffer
                    //
                    if (itemGroup.IsMatch(line))
                    {
                        conditionalBuffer = line;
                        continue;
                    }

                    if (conditionalBuffer != "")
                    {
                        if (line.Contains("</ItemGroup>"))
                        {
                            conditionalBuffer = "";
                            continue;
                        }
                    }

                    // include actions
                    // 
                    var t = reIncludeAction.Match(line);
                    if (t.Success)
                    {
                        if (t.Groups["file"].Value.Contains("OCC\\src"))
                        {
                            // skip line
                            continue;
                        }
                    }

                    // any skipped line before this
                    //
                    if (conditionalBuffer != "")
                    {
                        newProject.WriteLine(conditionalBuffer);
                        conditionalBuffer = "";
                    }

                    // AdditionalIncludeDirectories
                    if (line.Contains("<AdditionalIncludeDirectories"))
                    {
                        newProject.WriteLine(replaceAdditional);
                        continue;
                    }

                    // populate occ source
                    // <!-- occSource -->
                    if (line.Contains("<!-- occSource -->"))
                    {
                        sourcetagfound = true;
                        // populate source
                        foreach (var package in AllPackages())
                        {
                            newProject.WriteLine($"  <ItemGroup Label=\"{package.Name}\">");

                            foreach (var file in package.FileNames())
                            {
                                var action = OccSource.GetAction(file); // action for project file
                                var filename = Path.Combine(package.SourceRelativeFolder, file);
                                newProject.WriteLine($"    <{action} Include=\"{filename}\" />");
                            }
                            newProject.WriteLine($"  </ItemGroup>");
                        }
                        if (_extraHeaders.Any())
                        {
                            newProject.WriteLine($"  <ItemGroup Label=\"Extra\">");
                            foreach (var extraHeader in _extraHeaders)
                            {
                                var action = OccSource.GetAction(extraHeader.Name); // action for extra header in project file
                                newProject.WriteLine($"    <{action} Include=\"{extraHeader.Name}\" />");
                            }
                            newProject.WriteLine($"  </ItemGroup>");
                        }
                    }
                    newProject.WriteLine(line);
                }
            }
            return sourcetagfound;
        }

        private const string sourceFiles = "Source files";

        FileInfo csProjFilter
        {
            get
            {
                return new FileInfo(
                Path.Combine(GeometryProjectFolder.FullName,
                    "Xbim.Geometry.Engine.vcxproj.filters"
                ));
            }
        }

        FileInfo csProjFilterNew
        {
            get
            {
                return new FileInfo(csProjFilter.FullName + ".new");
            }
        }

        public string MakeProjectFilters()
        {
            var ret = "";
            // the approach chosen is to process the file as string in memory
            var filterProj = "";
            using (var filters = csProjFilter.OpenText())
            {
                filterProj = filters.ReadToEnd();
            }

            // remove filter groups
            var reRemoveFilter = new Regex("[ \t]*<Filter +.+?</Filter>[ \t]*[\r\n]*", RegexOptions.Singleline);
            filterProj = reRemoveFilter.Replace(filterProj, "");
            

            // remove occ source files
            var actions = new[] { "ClCompile", "None", "ClInclude" };
            foreach (var action in actions)
            {
                var reRemoveAction = new Regex($"[ \t]*<{action} +Include=\"(\\.\\\\)?OCC\\\\src\\\\.+?</{action}>[ \t]*[\r\n]*", RegexOptions.Singleline);
                filterProj = reRemoveAction.Replace(filterProj, "");
            }

            // remove empty ItemGroups
            var reRemoveEmptyItemGroups = new Regex("[ \t]*<ItemGroup[^>]*>[ \t\r\n]*?</ItemGroup>[ \t]*[\r\n]*", RegexOptions.Singleline);
            filterProj = reRemoveEmptyItemGroups.Replace(filterProj, "");


            StringBuilder sb = null;

            // add new occ content filters
            // 
            var hook = "<!-- Filters -->";
            if (!filterProj.Contains(hook))
            {
                ret += $"{hook} tag not found in ProjectFilters.\r\n";
            }
            else
            {
                sb = new StringBuilder();
                sb.AppendLine(hook);
                sb.AppendLine("  <ItemGroup>");
                AddFilterGroup(sb, $"{sourceFiles}");
                AddFilterGroup(sb, $"{sourceFiles}\\XbimGeometry");
                foreach (var lib in AllLibs())
                {
                    AddFilterGroup(sb, $"{sourceFiles}\\{lib.Name}");
                    foreach (var package in lib.Packages)
                    {
                        AddFilterGroup(sb, $"{sourceFiles}\\{lib.Name}\\{package.Name}");
                    }
                }
                if (_extraHeaders.Any())
                {
                    AddFilterGroup(sb, $"{sourceFiles}\\Extra");
                    var dist = _extraHeaders.Select(x => x.JustFolderName).Distinct();
                    foreach (var item in dist)
                    {
                        AddFilterGroup(sb, $"{sourceFiles}\\Extra\\{item}");
                    }
                }
                sb.Append("  </ItemGroup>");
                filterProj = filterProj.Replace(hook, sb.ToString());
            }

            // add new occ content files
            // 
            hook = "<!-- OccFiles -->";
            if (!filterProj.Contains(hook))
            {
                ret += $"{hook} tag not found in ProjectFilters.\r\n";
            }
            else
            {
                sb = new StringBuilder();

                sb.AppendLine(hook);
                sb.AppendLine("  <ItemGroup>");
                foreach (var lib in AllLibs())
                {
                    foreach (var package in lib.Packages)
                    {
                        foreach (var fileName in package.FileNames())
                        {
                            AddInclude(sb, package, fileName); // including file in filters
                        }
                    }

                }
                // now the extras
                //
                foreach (var extra in _extraHeaders)
                {
                    AddExtraIncludeFilter(sb, extra);
                }
                sb.Append("  </ItemGroup>");
                filterProj = filterProj.Replace(hook, sb.ToString());
            }
            // now write buffer
            using (var newFilters = csProjFilterNew.CreateText())
            {
                newFilters.Write(filterProj);
            }
            //
            return ret;
        }

        private void AddInclude(StringBuilder sb, OccPackage package, string file)
        {
            var action = OccSource.GetAction(file); // In filters file
            var filename = Path.Combine(package.SourceRelativeFolder, file);

            sb.AppendLine($"    <{action} Include=\"{filename}\">");
            sb.AppendLine($"      <Filter>{sourceFiles}\\{package.Lib.Name}\\{package.Name}</Filter>");
            sb.AppendLine($"    </{action}>");
        }

        private void AddExtraIncludeFilter(StringBuilder sb, Header file)
        {
            var filename = file.Name;
            var action = OccSource.GetAction(filename); // In filters file for extras

            sb.AppendLine($"    <{action} Include=\"{filename}\">");
            sb.AppendLine($"      <Filter>{sourceFiles}\\Extra\\{file.JustFolderName}</Filter>");
            sb.AppendLine($"    </{action}>");
        }


        private void AddFilterGroup(StringBuilder sb, string relativeFolderStructure)
        {
            sb.AppendLine($"    <Filter Include=\"{relativeFolderStructure}\">");
            sb.AppendLine($"      <UniqueIdentifier>{{{Guid.NewGuid()}}}</UniqueIdentifier>");
            sb.AppendLine($"    </Filter>");
        }

        internal void ReplaceSource(RichTextBoxReporter _r, bool justCopy = false)
        {
            // empty the existing directory
            if (OccDestFolder.Exists)
                Directory.Delete(OccDestFolder.FullName, true);
            // create again
            OccDestFolder.Create();

            var srcF = OccSourceFolder.FullName;
            var dstF = OccDestFolder.FullName;
            foreach (var extraPackage in _extraPackages)
            {
                extraPackage.CopySource(srcF, dstF, justCopy, _r);
            }
            foreach (var lib in AllLibs())
            {
                foreach (var package in lib.Packages)
                {
                    package.CopySource(srcF, dstF, justCopy, _r);
                }
            }
            foreach (var header in _extraHeaders)
            {
                header.CopySource(srcF, dstF, justCopy, _r);
            }
            System.Windows.MessageBox.Show("Done");
        }

        internal void RenameNew(bool chkCsProj, bool chkCsProjFilter)
        {
            if (chkCsProj)
                Rename(csProjNew, csProj);
            if (chkCsProjFilter)
                Rename(csProjFilterNew, csProjFilter);
        }

        private void Rename(FileInfo newFi, FileInfo oldFi)
        {
            if (!newFi.Exists)
            {
                System.Windows.MessageBox.Show("Error new file missing " + newFi.Name);
                return;
            }
            if (oldFi.Exists)
                oldFi.Delete();
            File.Move(newFi.FullName, oldFi.FullName);
        }

        internal void SetDefaultInitialisation()
        {
            var except = new[] { "CSF_.+" };
            var initlibs = new[]
            {
                "TKShHealing",
                "TKBool",
                "TKFillet",
                "TKMesh",
                "TKOffset"
            };

            foreach (var initlib in initlibs)
            {
                var p1 = this.GetLib(initlib);
                p1.Include(except);
            }

            // extra source - Exceptions to normal code management includes
            //
            // _extraPackages.Add(new OccPackage(this) { Name = "SHMessage" }); // needed for ShapeExtend\ShapeExtend.cxx
            // _extraHeaders.Add(new Header(@"OCC\src\Graphic3d\Graphic3d_Vec4.hxx")); // used in Quantity\Quantity_ColorRGBA.cxx
        }
    }
}
