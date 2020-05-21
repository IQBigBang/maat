using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Maat
{
    class CommandInterface
    {
        public static bool IsWindows { get { return RuntimeInformation.IsOSPlatform(OSPlatform.Windows); } }

        public static bool IsLinux { get { return RuntimeInformation.IsOSPlatform(OSPlatform.Linux); } }

        public static bool IsOSX { get { return RuntimeInformation.IsOSPlatform(OSPlatform.OSX); } }

        public static (string, string) RunCommand(string cmd, string[] args, string workingDir, bool printOutput = false)
        {
            if (!(IsWindows || IsLinux))
                ErrorReporter.Warning("The command inteface has not been tested on this OS and might not work");

            var process = new Process();
            var startInfo = new ProcessStartInfo(cmd, string.Join(" ", args))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = workingDir
            };
            
            process.StartInfo = startInfo;
            process.Start();

            if (printOutput)
            {
                process.OutputDataReceived += (sender, data) => Console.WriteLine(data.Data);
                process.BeginOutputReadLine();
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Console.Write(process.StandardError.ReadToEnd());
                ErrorReporter.Error("Command `{0}` failed.", cmd);
            }

            if (printOutput)
                return ("", process.StandardError.ReadToEnd());
            return (process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd());
        }
    }
}
