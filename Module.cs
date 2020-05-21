using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Maat
{
    public class Module
    {
        // path to the root file of the module (with all includes)
        public string RootFilePath { get; private set; }

        // the module name
        public string Name { get; private set; }

        // all files that belong to the module
        public List<string> Files { get; private set; }

        // modules this module depends on
        public List<string> Imports { get; private set; }

        public Module(string rootFilePath)
        {
            RootFilePath = new FileInfo(rootFilePath).FullName;
            var f = new SourceFile(RootFilePath);
            f.Parse();
            Name = f.ModuleName;
            Files = new List<string>();
            Imports = new List<string> { "std.core" }; // Implicitly imported
        }

        public void Parse()
        {
            // We go through all includes from all files
            // This is a list of yet unparsed included files
            var Includes = new List<string> { RootFilePath };

            while (Includes.Count != 0)
            {
                var newIncludes = new List<string>();

                foreach (var file in Includes)
                {
                    var parsed = new SourceFile(file);
                    parsed.Parse();

                    if (parsed.ModuleName != Name)
                        ErrorReporter.ErrorFL("File is included in module {1} but belongs in module {2}",
                            file, Name, parsed.ModuleName);

                    foreach (var include in parsed.Includes)
                    {
                        var actualInclude = Path.Combine(new FileInfo(file).DirectoryName, include);
                        if (Files.Contains(actualInclude))
                            ErrorReporter.NoteFL("Trying to include {0} into the module {1} but it is already included",
                                file, include, Name);

                        newIncludes.Add(actualInclude);
                    }

                    foreach (var import in parsed.Imports)
                    {
                        // Standard library imports are handled specially
                        if (import.StartsWith("std", 0))
                        {
                            Imports.Add(import);
                            continue;
                        }

                        var actualImport = Path.Combine(
                            new FileInfo(file).DirectoryName,
                            import.Replace('.', Path.DirectorySeparatorChar));

                        if (!Imports.Contains(actualImport))
                            Imports.Add(actualImport);
                    }
                }

                Includes.ForEach((x) => Files.Add(x));

                Includes.Clear();
                Includes = newIncludes;
            }
        }
    }
}
