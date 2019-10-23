using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuSpecHelper.Occ
{
    public class OccPackage
    {
        private OccLib _lib;
        private OccSource _source;

        public OccLib Lib
        {
            get { return _lib; }
        }
        
        public string SourceRelativeFolder => $"OCC\\src\\{Name}";

        public OccPackage(OccLib lib)
        {
            _lib = lib;
        }
        public OccPackage(OccSource source)
        {
            _source = source;
        }

        public IEnumerable<string> FileNames()
        {
            var v = (_lib != null)
                ? _lib.GetDir(Name)
                : _source.GetDir(Name);

            var extName = new FileInfo(Path.Combine(v.FullName, "FILES"));
            if (!extName.Exists)
                yield break;

            using (var read = extName.OpenText())
            {
                string line;
                while ((line = read.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    
                    yield return line;
                }
            }
        }   

        public string Name { get; set; }

        private static readonly Regex ReXxx = new Regex(@"^\.\wxx$", RegexOptions.Compiled);

        private static readonly Regex RegexNewLineFormat = new Regex(@"(\r\n?|\n)", RegexOptions.Compiled);

        static private bool NeedNewLineConversion(FileInfo src)
        {
            var ext = src.Extension;
            if (ReXxx.IsMatch(ext))
                return true;
            switch (ext)
            {
                case ".c":
                case ".h":
                    return true;
            }
            return false;
        }

        internal void CopySource(string srcF, string dstF, bool justCopy, RichTextBoxReporter _r)
        {
            // create package folder
            var dSrc = new DirectoryInfo(Path.Combine(srcF, Name));
            var dDest = new DirectoryInfo(Path.Combine(dstF, Name));
            foreach (var fileName in FileNames())
            {
                if (fileName.Contains(":::"))
                    continue;
                _r.AppendLine(fileName);
                var dest = new FileInfo(Path.Combine(dDest.FullName, fileName));
                var src = new FileInfo(Path.Combine(dSrc.FullName, fileName));
                DoCopy(justCopy, dest, src);
            }
        }

        static internal void DoCopy(bool justCopy, FileInfo dest, FileInfo src)
        {
            DirectoryInfo dDest = dest.Directory;
            if (!dDest.Exists)
                dDest.Create();
            if (justCopy) // just copy
                File.Copy(src.FullName, dest.FullName);
            else
            {
                var needNewLineConversion = NeedNewLineConversion(src);
                if (!needNewLineConversion)
                    File.Copy(src.FullName, dest.FullName);
                else
                {
                    using (var r = src.OpenText())
                    using (var w = dest.CreateText())
                    {
                        var all = r.ReadToEnd();
                        all = RegexNewLineFormat.Replace(all, "\r\n");
                        w.Write(all);
                    }
                }
            }
        }
    }
}
