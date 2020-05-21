﻿using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// Maat - an Egyptian goddess of order, a build system
namespace Maat
{
    class MainClass
    {

        public enum CToolChain
        {
            [YamlMember(Alias = "")]
            NoValue,
            [YamlMember(Alias = "gcc")]
            GCC,
            [YamlMember(Alias = "clang")]
            Clang
        }

        public enum GCChoice
        {
            [YamlMember(Alias = "gjduck")] // Default (and currently the only one) supported GC
            GJDuck,
            // TODO support BDWGC - however this is problematic on Windows
        }

        public struct ProjectFile
        {
            public string Name { get; set; }

            [YamlMember(Alias = "main")]
            public string MainFile { get; set; }

            [YamlMember(Alias = "ctoolchain")]
            public CToolChain CToolChain { get; set; }

            public GCChoice GC { get; set; }
        }

        private static List<string> AllCFiles = new List<string>();

        public static void Main(string[] args)
        {
            if (args.Length == 0)
                ErrorReporter.Error("action missing: ./maat [action] ");

            var action = args[0];

            if (action == "init")
                ActionInit();
            else if (action == "update")
                ActionUpdate(".");
            else if (action == "generate")
                ActionGenerate(".");
            else if (action == "build")
                ActionBuild(".");
            else if (action == "run")
                ActionRun(".");
            else if (action == "help")
                ActionHelp();
        }

        public static void ActionInit()
        {
            Console.WriteLine("Initializing a new Functional project...");
            Console.Write("Enter the project name: ");
            var projectName = Console.ReadLine(); // TODO: Check if it contains only alphanumerics

            if (Directory.Exists(projectName))
                ErrorReporter.Error("A directory named {0} already exists in this folder.", projectName);

            Console.WriteLine("Creating the project structure...");

            // Create the directory
            Directory.CreateDirectory(projectName);
            // Initialize project.yml
            string toolchain;

            if (CommandInterface.IsWindows)
            {
                Console.WriteLine("Detected Windows, defaulting to the MinGW compiler."); // TODO: MSVC support
                toolchain = "gcc";
            } else if (CommandInterface.IsOSX)
            {
                Console.WriteLine("Detected OSX, defaulting to the Clang compiler");
                toolchain = "clang";
            } else
            {
                Console.WriteLine("Detected Unix-like OS, defaulting to the GCC compiler");
                toolchain = "gcc";
            }

            File.WriteAllLines(Path.Combine(projectName, "project.yml"), new string[]
            {
                "---",
                "name: " + projectName,
                "main: main.f",
                "ctoolchain: " + toolchain
            });

            // Initialize src/main.f
            Directory.CreateDirectory(Path.Combine(projectName, "src"));
            File.WriteAllLines(Path.Combine(projectName, "src", "main.f"), new string[]
            {
                "module main",
                "",
                "import std.io",
                "",
                "main :: Nil",
                "main = writeStr \"Hello world!\"",
                ""
            });

            // Initialize build directory (just create)
            Directory.CreateDirectory(Path.Combine(projectName, "build"));

            // Initialize dist/bin and dist/std
            Directory.CreateDirectory(Path.Combine(projectName, "dist", "bin"));
            Directory.CreateDirectory(Path.Combine(projectName, "dist", "std"));

            ActionUpdate(projectName);
        }

        public static void ActionUpdate(string  projectDir)
        {
            var projectYaml = ParseYaml(projectDir);

            Console.WriteLine("Downloading the Functional compiler...");

            // clone the compiler
            CommandInterface.RunCommand("git", new string[] { "clone", "https://github.com/iqbigbang/functional.git" }, Path.Combine(projectDir, "dist"));

            Console.WriteLine("Building the Functional compiler...");

            CommandInterface.RunCommand("dotnet", new string[] { "publish", "-c", "release" }, Path.Combine(projectDir, "dist", "functional"));

            Console.WriteLine("Building the garbage collector...");

            CommandInterface.RunCommand(projectYaml.CToolChain.ToString(), new string[] { "-c", "-o", "../gc.o", "-O2", "-std=gnu99", "-DNODEBUG", "gc.c" }, Path.Combine(projectDir, "dist", "functional", "std", "gc"));

            Console.WriteLine("Copying files...");

            // copy the compiler files into dist/bin
            foreach (var file in
                Directory.EnumerateFiles(Path.Combine(projectDir, "dist", "functional", "bin", "release", "netcoreapp3.1", "publish")))
            {
                File.Copy(file, Path.Combine(projectDir, "dist", "bin", new FileInfo(file).Name), true);
            }

            // copy standard library files into dist/std
            foreach (var file in
                Directory.EnumerateFiles(Path.Combine(projectDir, "dist", "functional", "std")))
            {
                File.Copy(file, Path.Combine(projectDir, "dist", "std", new FileInfo(file).Name), true);
            }

            Console.WriteLine("Cleaning up...");

            // On Windows, the files in folders in .git/objects are set as readonly and the system won't let us delete them
            // so we have to manually change the attribute before deletion
            if (CommandInterface.IsWindows)
            {
                foreach (var folder in Directory.EnumerateDirectories(Path.Combine(projectDir, "dist", "functional", ".git", "objects")))
                {
                    foreach (var file in Directory.EnumerateFiles(folder))
                    {
                        var attributes = File.GetAttributes(file);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            attributes &= ~FileAttributes.ReadOnly;
                            File.SetAttributes(file, attributes);
                        }
                    }
                }
            }

            // remove dist/functional
            Directory.Delete(Path.Combine(projectDir, "dist", "functional"), true);
        }

        // Escapes `ninja` special characters: colon and dollar 
        private static string Escape(string s)
            => s.Replace("$", "$$").Replace(":", "$:");

        public static void ActionGenerate(string projectDir)
        {
            var projectYaml = ParseYaml(projectDir);
            var mainModule = new Module(new FileInfo(Path.Combine(projectDir, "src", projectYaml.MainFile)).FullName);

            mainModule.Parse();

            string compilerPath;
            if (CommandInterface.IsWindows)
                compilerPath = Path.Combine("dist", "bin", "Functional.exe");
            else
                compilerPath = Path.Combine("dist", "bin", "Functional");

            using var sw = new StreamWriter("build.ninja");
            sw.WriteLine("fbuildflags = ");
            if (projectYaml.CToolChain == CToolChain.GCC)
                sw.WriteLine("cbuildflags = -std=c89 -Wno-return-type");
            else if (projectYaml.CToolChain == CToolChain.Clang)
                sw.WriteLine("cbuildflags = -fcolor-diagnostics -std=c89 -Wno-return-type ");
            sw.WriteLine("builddir = build/");
            sw.WriteLine("rule fcc");
            sw.WriteLine("    command = {0} $in -m $module -b $builddir -i $imports $fbuildflags", compilerPath);
            sw.WriteLine("    description = compile module $module");
            sw.WriteLine("rule cc");
            sw.WriteLine("    command = {0} $cbuildflags -I$builddir $in dist/std/gc.o -o $out", 
                projectYaml.CToolChain == CToolChain.Clang ? "clang" : "gcc");
            sw.WriteLine("    description = link executable $out");
            sw.WriteLine();

            GenerateBuildRule(sw, mainModule, "build");

            sw.WriteLine("build {0}: cc {1}",
                Escape(projectYaml.Name), Escape(string.Join(" ", AllCFiles)));
            sw.WriteLine("default {0}", Escape(projectYaml.Name));
        }

        public static void ActionBuild(string projectDir)
        {
            ActionGenerate(projectDir);

            string ninjaName = CommandInterface.IsWindows ? "ninja.exe" : "ninja";

            // Download ninja if running for the first time
            if (!File.Exists(Path.Combine(projectDir, "dist", "bin", ninjaName)))
            {
                Console.WriteLine("Downloading ninja... (this only happens once)");

                var url = CommandInterface.IsWindows ? "https://github.com/ninja-build/ninja/releases/download/v1.10.0/ninja-win.zip"
                        : CommandInterface.IsOSX ? "https://github.com/ninja-build/ninja/releases/download/v1.10.0/ninja-mac.zip"
                        : "https://github.com/ninja-build/ninja/releases/download/v1.10.0/ninja-linux.zip";

                using var client = new HttpClient();
                var ninja = client.GetStreamAsync(url);
                ninja.Wait();

                using (var archive = File.Open(Path.Combine(projectDir, "dist", "bin", "ninja.zip"), FileMode.OpenOrCreate, FileAccess.Write))
                {
                    ninja.Result.CopyTo(archive);
                }

                // Extract the archive
                ZipFile.ExtractToDirectory(Path.Combine(projectDir, "dist", "bin", "ninja.zip"), Path.Combine(projectDir, "dist", "bin"));
                // Remove the archive
                File.Delete(Path.Combine(projectDir, "dist", "bin", "ninja.zip"));
            }

            CommandInterface.RunCommand(Path.Combine(projectDir, "dist", "bin", ninjaName), new string[0], projectDir, true);
        }

        public static void ActionRun(string projectDir)
        {
            ActionBuild(projectDir);
            var projectYaml = ParseYaml(projectDir);

            var projectExecutable = Path.Combine(projectDir, projectYaml.Name);
            if (CommandInterface.IsWindows)
                projectExecutable += ".exe";

            CommandInterface.RunCommand(projectExecutable, new string[] { }, ".", true);
        }

        public static void ActionHelp()
        {
            Console.WriteLine(@"Maat 0.1: The simple (and buggy) build system for Functional

Syntax: maat [action] [flags]

Supported actions:

    init - Create a new project in the current directory
           Interactive, asks for configuration
           After creation calls `maat update`
           Flags: none

    update - Downloads the newest Functional compiler and sets it up
             Logs progress to the terminal
             Flags: none

    generate - Generates a ninja build file in the project root from the sources
               Flags: none

    build - Calls `maat generate` and then calls `ninja`
            Ninja is automatically downloaded to dist/bin
            Flags: none
    
    run - Calls `maat build` and then runs the built executable
          Flags: none

    help - Prints this info
");
        }

        private static ProjectFile ParseYaml(string projectDir)
        {
            if (!new DirectoryInfo(projectDir).Exists)
            {
                ErrorReporter.Error("Project folder does not exist");
                return default;
            }

            var projectFile =
                new DirectoryInfo(projectDir).GetFiles().FirstOrDefault((fi) => fi.Name == "project.yml" || fi.Name == "project.yaml");

            if (projectFile is null)
            {
                ErrorReporter.ErrorFL("project file not found", "project.yml");
                return default;
            }

            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var deserialized = deserializer.Deserialize<ProjectFile>(projectFile.OpenText());

            // Enums default to the first value so take care of that
            if (deserialized.CToolChain == CToolChain.NoValue)
            {
                ErrorReporter.ErrorFL("required value `ctoolchain` not found", "project.yml");
                return default;
            }
            return deserialized;
        }

        private static void GenerateBuildRule(StreamWriter output, Module module, string buildDir)
        {
            // Files generated by the compiler: c file, functional header file, c header file
            output.Write("build {0} {1} {2}: fcc",
                Escape(Path.Combine(buildDir, module.Name + ".c")), 
                Escape(Path.Combine(buildDir, module.Name + ".fh")), 
                Escape(Path.Combine(buildDir, module.Name + ".h")));

            if (!AllCFiles.Contains(Path.Combine(buildDir, module.Name + ".c")))
                AllCFiles.Add(Path.Combine(buildDir, module.Name + ".c"));

            foreach (var file in module.Files)
                output.Write(" {0} ", Escape(file));

            // Those imports will have recursively generated build rules
            var parsedImports = new List<Module>();
            // Those imports will be passed in the `import` flag
            var importsFlag = new List<string>();

            foreach (var import in module.Imports)
            {
                if (import.StartsWith("std", 0))
                {
                    var fileWithoutExtension = Path.Combine("dist", "std", import.Substring(4));
                    output.Write(" {0}.fh ", Escape(fileWithoutExtension));
                    if (!AllCFiles.Contains(fileWithoutExtension + ".c"))
                        AllCFiles.Add(fileWithoutExtension + ".c");
                    importsFlag.Add(import);
                    continue;
                }

                var moduleImport = new Module(import + ".f");
                moduleImport.Parse();

                output.Write(" {0} ", Escape(Path.Combine(buildDir, moduleImport.Name + ".fh")));
                parsedImports.Add(moduleImport);
                importsFlag.Add(moduleImport.Name);
            }

            output.WriteLine();
            output.WriteLine("    module = {0}", module.Name);
            output.WriteLine("    imports = {0}", string.Join(",", importsFlag));
            output.WriteLine();

            parsedImports.ForEach((x) => GenerateBuildRule(output, x, buildDir));
        }

    }
}