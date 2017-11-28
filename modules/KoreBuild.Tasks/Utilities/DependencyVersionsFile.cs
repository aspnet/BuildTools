// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace KoreBuild.Tasks.Utilities
{
    public class DependencyVersionsFile
    {
        public const string PackageVersionsLabel = "Package Versions";

        private readonly Dictionary<string, string> _versionVariables
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly SortedDictionary<string, ProjectPropertyElement> _versionElements
            = new SortedDictionary<string, ProjectPropertyElement>(StringComparer.OrdinalIgnoreCase);

        private readonly ProjectRootElement _document;
        private ProjectPropertyGroupElement _versionsPropGroup;

        public DependencyVersionsFile(ProjectRootElement xDocument)
        {
            _document = xDocument;
        }

        public static string GetVariableName(string packageId)
        {
            var sb = new StringBuilder();
            var first = true;
            foreach (var ch in packageId)
            {
                if (!char.IsLetterOrDigit(ch))
                {
                    first = true;
                    continue;
                }

                if (first)
                {
                    first = false;
                    sb.Append(char.ToUpperInvariant(ch));
                }
                else
                {
                    sb.Append(ch);
                }
            }
            sb.Append("PackageVersion");
            return sb.ToString();
        }

        public static DependencyVersionsFile Create(bool addOverrideImport, string[] additionalImports = null)
        {
            var projectRoot = ProjectRootElement.Create(NewProjectFileOptions.None);

            projectRoot.AddPropertyGroup().AddProperty("MSBuildAllProjects", "$(MSBuildAllProjects);$(MSBuildThisFileFullPath)");

            var packageVersions = projectRoot.AddPropertyGroup();
            packageVersions.Label = PackageVersionsLabel;

            if (additionalImports != null)
            {
                foreach (var item in additionalImports)
                {
                    var import = projectRoot.AddImport(item);
                    import.Condition = $"Exists('{item}')";
                }
            }

            if (addOverrideImport)
            {
                var import = projectRoot.AddImport("$(DotNetPackageVersionPropsPath)");
                import.Condition = " '$(DotNetPackageVersionPropsPath)' != '' ";
            }

            var file = new DependencyVersionsFile(projectRoot);
            file._versionsPropGroup = packageVersions;
            return file;
        }

        public static bool TryLoad(string sourceFile, out DependencyVersionsFile file)
        {
            try
            {
                file = Load(sourceFile);
                return true;
            }
            catch
            {
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
                    file._versionElements[child.Name.ToString()] = child;
                }
            }

            return file;
        }

        public bool HasVersionsPropertyGroup() => _versionsPropGroup != null;

        // copying is required so calling .Set while iterating on this doesn't raise an InvalidOperationException
        public IReadOnlyDictionary<string, string> VersionVariables
            => new Dictionary<string, string>(_versionVariables, StringComparer.OrdinalIgnoreCase);

        public ProjectPropertyElement Set(string variableName, string version)
        {
            _versionVariables[variableName] = version;
            if (!_versionElements.TryGetValue(variableName, out var element))
            {
                element = _document.CreatePropertyElement(variableName);
                _versionElements.Add(variableName, element);
            }
            element.Value = version;
            return element;
        }

        public void Save(string filePath)
        {
            if (_versionsPropGroup == null)
            {
                _versionsPropGroup = _document.AddPropertyGroup();
                _versionsPropGroup.Label = PackageVersionsLabel;
            }

            _versionsPropGroup.RemoveAllChildren();
            foreach (var item in _versionElements.Values)
            {
                _versionsPropGroup.AppendChild(item);
            }

            _document.Save(filePath, Encoding.UTF8);
        }
    }
}
