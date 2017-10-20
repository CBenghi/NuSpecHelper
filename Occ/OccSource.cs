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
                case ".h":
                    return "ClInclude";
                case ".cxx":
                case ".c":
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
            var p = new OccLib(this) { Name = libName };
            _libs.Add(libName, p);
            return p;
        }

        List<OccPackage> _extraPackages = new List<OccPackage>();

        public OccSource(string occSourceFolder, string xbimGeometryProjectFolder)
        {
            OccSourceFolder = new DirectoryInfo(occSourceFolder);
            GeometryProjectFolder = new DirectoryInfo(xbimGeometryProjectFolder);
            OccDestFolder = new DirectoryInfo(Path.Combine(
                xbimGeometryProjectFolder,
                "OCC\\src\\"));

            // extra source
            var p = new OccPackage(this) {Name = "SHMessage"};
            _extraPackages.Add(p);
        }
        
        public IEnumerable<OccLib> AllLibs()
        {
            return _libs.Values;
        }

        public IEnumerable<OccPackage> AllPackages()
        {
            return AllLibs().SelectMany(lib => lib.Packages);
        }

        public void MakeProject()
        {
            var csProj = new FileInfo(
                Path.Combine(GeometryProjectFolder.FullName,
                    "Xbim.Geometry.Engine.vcxproj"
                ));

            var csProjNew = new FileInfo(
                Path.Combine(GeometryProjectFolder.FullName,
                    "Xbim.Geometry.Engine.vcxproj.new"
                ));

            var packageFolders = AllPackages().Select(x => x.SourceRelativeFolder).ToArray();
            var packageFoldersList = string.Join(";", packageFolders);
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
                        // populate source
                        foreach (var package in AllPackages())
                        {
                            newProject.WriteLine($"  <ItemGroup Label=\"{package.Name}\">");

                            foreach (var file in package.FileNames())
                            {
                                var action = OccSource.GetAction(file);
                                var filename = Path.Combine(package.SourceRelativeFolder, file);
                                newProject.WriteLine($"    <{action} Include=\"{filename}\" />");
                            }
                            newProject.WriteLine($"  </ItemGroup>");
                        }
                    }
                    newProject.WriteLine(line);
                }
            }
        }

        private const string sourceFiles = "Source files";

        public void MakeProjectFilters()
        {
            var csProj = new FileInfo(
                Path.Combine(GeometryProjectFolder.FullName,
                    "Xbim.Geometry.Engine.vcxproj.filters"
                ));

            var csProjNew = new FileInfo(
                Path.Combine(GeometryProjectFolder.FullName,
                    "Xbim.Geometry.Engine.vcxproj.filters.new"
                ));

            // the approach chosen is to process the file as string in memory
            var filterProj = "";
            using (var filters = csProj.OpenText())
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

            // add new occ content filters
            // 
            var hook = "<!-- Filters -->";
            var sb = new StringBuilder();
            sb.AppendLine(hook);
            sb.AppendLine("  <ItemGroup>");
            AddFilter(sb, $"{sourceFiles}");
            AddFilter(sb, $"{sourceFiles}\\XbimGeometry");
            foreach (var lib in AllLibs())
            {
                AddFilter(sb, $"{sourceFiles}\\{lib.Name}");
                foreach (var package in lib.Packages)
                {
                    AddFilter(sb, $"{sourceFiles}\\{lib.Name}\\{package.Name}");
                }
            }
            sb.Append("  </ItemGroup>");
            filterProj = filterProj.Replace(hook, sb.ToString());

            // add new occ content files
            // 
            hook = "<!-- OccFiles -->";
            sb = new StringBuilder();
            sb.AppendLine(hook);
            sb.AppendLine("  <ItemGroup>");
            foreach (var lib in AllLibs())
            {
                foreach (var package in lib.Packages)
                {
                    foreach (var fileName in package.FileNames())
                    {
                        AddInclude(sb, package, fileName);
                    }
                }
            }
            sb.Append("  </ItemGroup>");
            filterProj = filterProj.Replace(hook, sb.ToString());

            // now write buffer
            using (var newFilters = csProjNew.CreateText())
            {
                newFilters.Write(filterProj);
            }
            //
            var done = false;
        }

        private void AddInclude(StringBuilder sb, OccPackage package, string file)
        {
            var action = OccSource.GetAction(file);
            var filename = Path.Combine(package.SourceRelativeFolder, file);

            sb.AppendLine($"    <{action} Include=\"{filename}\">");
            sb.AppendLine($"      <Filter>{sourceFiles}\\{package.Lib.Name}\\{package.Name}</Filter>");
            sb.AppendLine($"    </{action}>");
        }

        private void AddFilter(StringBuilder sb, string relativeFolderStructure)
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
        }       
    }
}
