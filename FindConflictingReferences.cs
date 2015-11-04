using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using NuSpecHelper;

namespace FindConflictingReference
{
    public class Reference
    {
        public AssemblyName Assembly { get; set; }
        public AssemblyName ReferencedAssembly { get; set; }
    }

    public class FindConflictingReferenceFunctions
    {
        static public IEnumerable<IGrouping<string, Reference>> FindReferencesWithTheSameShortNameButDiffererntFullNames(List<Reference> references)
        {
            return from reference in references
                   group reference by reference.ReferencedAssembly.Name
                       into referenceGroup
                       where referenceGroup.ToList().Select(reference => reference.ReferencedAssembly.FullName).Distinct().Count() > 1
                       select referenceGroup;
        }

        static public List<Reference> GetReferencesFromAllAssemblies(List<Assembly> assemblies)
        {
            var references = new List<Reference>();
            foreach (var assembly in assemblies)
            {
                if (assembly == null)
                    continue;
                foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
                {
                    references.Add(new Reference
                    {
                        Assembly = assembly.GetName(),
                        ReferencedAssembly = referencedAssembly
                    });
                }
            }
            return references;
        }

        static public List<Assembly> GetAllAssemblies(string path)
        {
            var files = new List<FileInfo>();
            var directoryToSearch = new DirectoryInfo(path);
            files.AddRange(directoryToSearch.GetFiles("*.dll", SearchOption.AllDirectories));
            files.AddRange(directoryToSearch.GetFiles("*.exe", SearchOption.AllDirectories));
            return files.ConvertAll(file =>
            {
                try
                {
                    Assembly asm = Assembly.LoadFile(file.FullName);
                    return asm;
                }
                catch (System.BadImageFormatException)
                {
                    return null;
                }

            });
        }
    }

    // Based on https://gist.github.com/brianlow/1553265
    class ConflictFinder
    {
        internal static void FindConflicts(RichTextBoxReporter output, string path)
        {
            var assemblies = FindConflictingReferenceFunctions.GetAllAssemblies(path);

            var references = FindConflictingReferenceFunctions.GetReferencesFromAllAssemblies(assemblies);

            var groupsOfConflicts = FindConflictingReferenceFunctions.FindReferencesWithTheSameShortNameButDiffererntFullNames(references);

            foreach (var group in groupsOfConflicts)
            {
                output.AppendLine(String.Format("Possible conflicts for {0}:", group.Key), Brushes.OrangeRed);

                var tmpDic = new Dictionary<string, List<string>>();
                foreach (var reference in group)
                {
                    List<string> dicItem;
                    if ( tmpDic.TryGetValue(reference.ReferencedAssembly.FullName, out dicItem))
                        dicItem.Add(reference.Assembly.Name);
                    else
                    {
                        tmpDic.Add(reference.ReferencedAssembly.FullName, new List<string>() {reference.Assembly.Name});
                    }
                }
                foreach (var pair in tmpDic)
                {
                    output.AppendLine("- " + pair.Key, Brushes.Blue);
                    foreach (var item in pair.Value)
                    {
                        output.AppendLine("  - " + item);
                    }
                }
            }
        }        
    }
}
