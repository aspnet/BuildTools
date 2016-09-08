using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DependenciesPackager
{
    internal class ProcessRunner
    {
        private const int DefaultTimeOutMinutes = 20;

        private readonly string _arguments;
        private readonly string _exePath;
        private readonly IDictionary<string, string> _environment = new Dictionary<string, string>();
        private Process _process = null;
        private object _writeLock = new object();
        private string _workingDirectory;

        public ProcessRunner(
            string exePath,
            string arguments)
        {
            _exePath = exePath;
            _arguments = arguments;
            _workingDirectory = Path.GetDirectoryName(_exePath);
        }

        public Action<string> OnError { get; set; } = s => { };

        public Action<string> OnOutput { get; set; } = s => { };

        public int ExitCode => _process.ExitCode;

        public int TimeOut { get; set; } = DefaultTimeOutMinutes * 60 * 1000;

        public ProcessRunner WriteErrorsToConsole()
        {
            OnError = s => Console.WriteLine(s);
            return this;
        }

        public ProcessRunner WriteOutputToConsole()
        {
            OnOutput = s => Console.WriteLine(s);
            return this;
        }

        public ProcessRunner WriteErrorsToStringBuilder(StringBuilder builder, string indentation)
        {
            OnError = s =>
            {
                lock (_writeLock)
                {
                    builder.AppendLine(indentation + s);
                }
            };
            return this;
        }

        public ProcessRunner WriteOutputToStringBuilder(StringBuilder builder, string indentation)
        {
            OnOutput = s =>
            {
                lock (_writeLock)
                {
                    builder.AppendLine(indentation + s);
                }
            };
            return this;
        }

        public ProcessRunner WithWorkingDirectory(string workingDirectory)
        {
            _workingDirectory = workingDirectory;
            return this;
        }

        public ProcessRunner AddEnvironmentVariable(string name, string value)
        {
            _environment.Add(name, value);
            return this;
        }

        public ProcessRunner WithTimeOut(int minutes)
        {
            TimeOut = minutes * 60 * 1000;
            return this;
        }

        public int Run()
        {
            if (_process != null)
            {
                throw new InvalidOperationException("The process has already been started.");
            }

            var processInfo = CreateProcessInfo();
            _process = new Process();
            _process.StartInfo = processInfo;
            _process.EnableRaisingEvents = true;
            _process.ErrorDataReceived += (s, e) => OnError(e.Data);
            _process.OutputDataReceived += (s, e) => OnOutput(e.Data);
            _process.Start();

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _process.WaitForExit(TimeOut);
            if (!_process.HasExited)
            {
                _process.Dispose();
                throw new InvalidOperationException($"Process {_process.ProcessName} timed out");
            }

            return _process.ExitCode;
        }

        private ProcessStartInfo CreateProcessInfo()
        {
            var processInfo = new ProcessStartInfo(_exePath, _arguments);
            foreach (var variable in _environment)
            {
                processInfo.Environment.Add(variable.Key, variable.Value);
            }

            processInfo.WorkingDirectory = _workingDirectory;
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            return processInfo;
        }
    }
}
