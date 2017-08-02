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

        public OccSource(string occSourceFolder, string xbimGeometryProjectFolder)
        {
            OccSourceFolder = new DirectoryInfo(occSourceFolder);
            GeometryProjectFolder = new DirectoryInfo(xbimGeometryProjectFolder);
            OccDestFolder = new DirectoryInfo(Path.Combine(
                xbimGeometryProjectFolder,
                "OCC\\src\\"));
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



            var replaceAdditional = "      <AdditionalIncludeDirectories>$(CSF_OPT_INC);%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>";
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
                    }
                    newProject.WriteLine(line);
                }
            }
        }
    }
}
