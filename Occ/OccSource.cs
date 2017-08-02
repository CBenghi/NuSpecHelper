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

        public void MakeProject()
        {
            var csProj = new FileInfo(
                "C:\\Users\\Claudio\\Dev\\Xbim3\\XbimGeometry3\\Xbim.Geometry.Engine\\Xbim.Geometry.Engine.vcxproj");
            var xbim3new = new FileInfo(
                "C:\\Users\\Claudio\\Dev\\Xbim3\\XbimGeometry3\\Xbim.Geometry.Engine\\Xbim.Geometry.Engine.vcxproj.new");


            var findFile = "<Action +Include=\"filename\">[\\s\\n\\r]*<Filter>([\\\\\\w\\s]+)</Filter>";

            var vals = new List<string>();

            var re = new Regex("<(?<action>[\\S]+) *Include=\"(?<file>[^\"]+?)(\\.(?<ext>[\\w]+))?\" */>");

            using (var write = xbim3new.CreateText())
            using (var read = csProj.OpenText())
            {
                string line;
                while ((line = read.ReadLine()) != null)
                {
                    var t = re.Match(line);
                    if (t.Success)
                    {
                        var str = "Ext: " + t.Groups["ext"].Value + " -> " + t.Groups["action"].Value;

                        if (t.Groups["ext"].Value == "")
                        {
                            Debug.WriteLine("File : " + t.Groups["file"].Value);
                        }

                        if (vals.Contains(str))
                            continue;
                        Debug.WriteLine(str);
                        vals.Add(str);
                    }
                }
            }
        }

    }
}
