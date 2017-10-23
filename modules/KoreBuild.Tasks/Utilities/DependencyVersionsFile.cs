// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Utilities;

namespace KoreBuild.Tasks.Utilities
{
    public class DependencyVersionsFile
    {
        public const string PackageVersionsLabel = "Package Versions";

        private readonly Dictionary<string, string> _versionVariables
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly ProjectRootElement _document;
        private ProjectPropertyGroupElement _versionsPropGroup;

        public DependencyVersionsFile(ProjectRootElement xDocument)
        {
            _document = xDocument;
        }

        public static bool TryLoad(string sourceFile, TaskLoggingHelper Log, out DependencyVersionsFile file)
        {
            try
            {
                file = Load(sourceFile);
                return true;
            }
            catch (InvalidProjectFileException ex)
            {
                Log.LogError(null, null, null, sourceFile, 0, 0, 0, 0, message: "Invalid MSBuild file: " + ex.Message);
                file = null;
                return false;
            }
        }

        public static DependencyVersionsFile Load(string sourceFile)
        {
            var project = ProjectRootElement.Open(sourceFile, ProjectCollection.GlobalProjectCollection, preserveFormatting: true);
            return Load(project);
        }

        public static DependencyVersionsFile Load(ProjectRootElement document)
        {
            var file = new DependencyVersionsFile(document);

            var propGroups = file._document.PropertyGroups;
            foreach (var propGroup in propGroups)
            {
                var attr = propGroup.Label;
                if (attr != null && attr.Equals(PackageVersionsLabel, StringComparison.OrdinalIgnoreCase))
                {
                    file._versionsPropGroup = propGroup;
                    break;
                }
            }

            if (file._versionsPropGroup != null)
            {
                foreach (var child in file._versionsPropGroup.Properties)
                {
                    file._versionVariables[child.Name.ToString()] = child.Value?.Trim() ?? string.Empty;
                }
            }

            return file;
        }

        public bool HasVersionsPropertyGroup() => _versionsPropGroup != null;

        public IReadOnlyDictionary<string, string> VersionVariables => _versionVariables;

        public void Set(string variableName, string version)
        {
            if (_versionsPropGroup == null)
            {
                _versionsPropGroup = _document.AddPropertyGroup();
                _versionsPropGroup.Label = PackageVersionsLabel;
            }

            _versionsPropGroup.SetProperty(variableName, version);
        }

        public void Save(string filePath)
        {
            _document.Save(filePath, Encoding.UTF8);
        }
    }
}
