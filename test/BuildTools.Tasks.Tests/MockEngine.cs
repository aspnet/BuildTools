// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections;
using Microsoft.Build.Framework;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace BuildTools.Tasks.Tests
{
    public class MockEngine : IBuildEngine5
    {
        private readonly ITestOutputHelper _output;

        public MockEngine()
            : this(null)
        {
        }

        public MockEngine(ITestOutputHelper output)
        {
            _output = output;
        }

        public bool ThrowOnError { get; set; } = true;
        public ICollection<BuildMessageEventArgs> Messages { get; } = new List<BuildMessageEventArgs>();
        public ICollection<BuildWarningEventArgs> Warnings { get; } = new List<BuildWarningEventArgs>();

        public bool IsRunningMultipleNodes => false;

        public bool ContinueOnError => false;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => "<test>";

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            Messages.Add(e);
            if (_output != null)
            {
                _output.WriteLine($"MESSAGE: {e.Message}");
            }
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            Warnings.Add(e);
            if (_output != null)
            {
                _output.WriteLine($"WARNING: {e.Message}");
            }
        }

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            if (_output != null)
            {
                _output.WriteLine($"ERROR: {e.Message}");
            }

            if (ThrowOnError)
            {
                throw new XunitException("Task produced error: " + e.Message);
            }
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            if (_output != null)
            {
                _output.WriteLine($"CUSTOM: {e.ToString()}");
            }
        }

        #region NotImplemented

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
        {
            throw new NotImplementedException();
        }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }

        public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs)
        {
            throw new NotImplementedException();
        }

        public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
        {
            throw new NotImplementedException();
        }

        public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            throw new NotImplementedException();
        }

        public void LogTelemetry(string eventName, IDictionary<string, string> properties)
        {
            throw new NotImplementedException();
        }

        public void Reacquire()
        {
            throw new NotImplementedException();
        }

        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
        {
            throw new NotImplementedException();
        }

        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            throw new NotImplementedException();
        }

        public void Yield()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
