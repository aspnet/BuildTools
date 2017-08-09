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

        public TestApp(string templateDir, string toolsSource, string source, string workDir)
        {
            WorkingDirectory = workDir;
            _toolsSource = toolsSource;
            Directory.CreateDirectory(workDir);
            CopyRecursive(templateDir, workDir);
            CopyRecursive(source, workDir);
        }

        public string WorkingDirectory { get; }

        public async Task<int> ExecuteBuild(ITestOutputHelper output, params string[] args)
        {
            output.WriteLine("Starting in " + WorkingDirectory);
            void Write(object sender, DataReceivedEventArgs e)
            {
                output.WriteLine(e.Data ?? string.Empty);
            }

            var arguments = new List<string>();
            string cmd;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                cmd = "cmd.exe";
                arguments.Add("/C");
                arguments.Add(@".\build.cmd");
            }
            else
            {
                cmd = "bash";
                arguments.Add("./build.sh");
            }

            arguments.AddRange(new[]
            {
                "-ToolsSource", _toolsSource,
                "-Update"
            });

            arguments.Add("/v:n");
            arguments.AddRange(args);

            var process = new Process
            {
                StartInfo =
                {
                    FileName = cmd,
                    Arguments = ArgumentEscaper.EscapeAndConcatenate(arguments),
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    Environment =
                    {
                        ["PATH"] = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
                    },
                    WorkingDirectory = WorkingDirectory,
                },
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += Write;
            process.ErrorDataReceived += Write;
            var tcs = new TaskCompletionSource<object>();
            process.Exited += (o, e) => tcs.TrySetResult(true);
            output.WriteLine($"Starting: {process.StartInfo.FileName} {process.StartInfo.Arguments}");
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            if (!process.HasExited)
            {
                await tcs.Task;
            }

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
