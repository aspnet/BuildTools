// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Xunit.Abstractions;

namespace KoreBuild.FunctionalTests
{
    public class TestApp : IDisposable
    {
        private readonly string _toolsSource;
        private readonly string _logFile;

        public TestApp(string templateDir, string toolsSource, string source, string workDir, string logFile)
        {
            WorkingDirectory = workDir;
            _logFile = logFile;
            _toolsSource = toolsSource;
            Directory.CreateDirectory(workDir);
            CopyRecursive(templateDir, workDir);
            CopyRecursive(source, workDir);
            var db = Path.Combine(AppContext.BaseDirectory, "TestResources", "Directory.Build.targets");
            File.Copy(db, Path.Combine(workDir, "Directory.Build.targets"), overwrite: true);
        }

        public string WorkingDirectory { get; }

        public int ExecuteRun(ITestOutputHelper output, string[] koreBuildArgs, params string[] commandArgs)
        {
            return ExecuteScript(output, "run", koreBuildArgs, commandArgs);
        }

        public int ExecuteBuild(ITestOutputHelper output, params string[] commandArgs)
        {
            return ExecuteScript(output, "build", new string[0], commandArgs);
        }

        private int ExecuteScript(ITestOutputHelper output, string script, string[] koreBuildArgs, params string[] commandArgs)
        {
            output.WriteLine("Starting in " + WorkingDirectory);

            var arguments = new List<string>();
            string cmd;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                cmd = "cmd.exe";
                arguments.Add("/C");
                arguments.Add($@".\{script}.cmd");
            }
            else
            {
                cmd = "bash";
                arguments.Add($"./{script}.sh");
            }

            arguments.AddRange(koreBuildArgs);

            arguments.AddRange(new[]
            {
                "-ToolsSource", _toolsSource,
                "-Reinstall",
            });

            arguments.AddRange(commandArgs);

            arguments.Add("/bl:" + _logFile);

            var psi = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = ArgumentEscaper.EscapeAndConcatenate(arguments),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,

                WorkingDirectory = WorkingDirectory,
            };

            return Run(output, psi);
        }

        public int Run(ITestOutputHelper output, ProcessStartInfo psi)
        {
            void Write(object sender, DataReceivedEventArgs e)
            {
                output.WriteLine(e.Data ?? string.Empty);
            }

            psi.UseShellExecute = false;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            psi.Environment["PATH"] = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");

            // set this to suppress TC service messages such as ##teamcity[importData]
            psi.Environment["TEAMCITY_VERSION"] = "";

            var process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += Write;
            process.ErrorDataReceived += Write;
            output.WriteLine($"Starting: {process.StartInfo.FileName} {process.StartInfo.Arguments}");
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            process.WaitForExit(1000 * 60 * 3);

            process.OutputDataReceived -= Write;
            process.ErrorDataReceived -= Write;
            return process.ExitCode;
        }

        private static void CopyRecursive(string srcDir, string destDir)
        {
            foreach (var srcFileName in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
            {
                var destFileName = Path.Combine(destDir, srcFileName.Substring(srcDir.Length).TrimStart(new[] { Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar }));
                Directory.CreateDirectory(Path.GetDirectoryName(destFileName));
                File.Copy(srcFileName, destFileName);
            }
        }

        public void Dispose()
        {
            Directory.Delete(WorkingDirectory, recursive: true);
        }
    }
}
