using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Maat
{
    public class SourceFile
    {
        // full path to the file
        public string FilePath { get; private set; }
        // the name of the file
        public string FileName { get; private set; }
        // name of the module this file belongs in
        public string ModuleName { get; private set; }
        // modules this file depends on
        public List<string> Imports { get; private set; }
        // files that are included in the current module
        public List<string> Includes { get; private set; }

        public SourceFile(string filePath)
        {
            FilePath = filePath;
            if (!FilePath.EndsWith(".f"))
                FilePath += ".f";

            if (!File.Exists(FilePath))
                ErrorReporter.Error("file {0} does not exist", FilePath);

            FileName = new FileInfo(FilePath).Name;
            ModuleName = "";
            Imports = new List<string>();
            Includes = new List<string>();
        }

        public void Parse()
        {
            // Read only the first sixty lines
            foreach (var line in File.ReadLines(FilePath).Take(60))
            {
                if (line.StartsWith("module ", 0))
                {
                    if (ModuleName != "")
                        ErrorReporter.ErrorFL("file can contain only one `module` statement", FileName);

                    var ind = line.IndexOf(' ', 7);
                    if (ind == -1) ind = line.Length;
                    ModuleName = line.Substring(7, ind - 7);

                    if (ModuleName.Any((c) => !char.IsLetterOrDigit(c) && c != '.'))
                        ErrorReporter.ErrorFL("module name can contain only letters, digits and dots", FileName);
                }
                else if (line.StartsWith("import ", 0))
                {
                    var ind = line.IndexOf(' ', 7);
                    if (ind == -1) ind = line.Length;
                    var import = line.Substring(7, ind - 7);

                    if (import.Any((c) => !char.IsLetterOrDigit(c) && c != '.'))
                        ErrorReporter.ErrorFL("an import can contain only letters, digits and dots", FileName);
                    Imports.Add(import);
                }
                else if (line.StartsWith("include \"", 0))
                {
                    var ind = line.IndexOf('"', 9);
                    if (ind == -1) ind = line.Length;
                    Includes.Add(line.Substring(9, ind - 9));
                }
            }

            if (ModuleName == "")
                ErrorReporter.ErrorFL("no `module` statement found", FileName);
        }
    }
}
