// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace KoreBuild.Tasks.Utilities
{
    public class DependencyVersionsFile
    {
        public const string PackageVersionsLabel = "Package Versions";

        public const string AutoPackageVersionsLabel = "Package Versions: Auto";
        public const string PinnedPackageVersionsLabel = "Package Versions: Pinned";

        private readonly Dictionary<string, PackageVersionVariable> _versionVariables
            = new Dictionary<string, PackageVersionVariable>(StringComparer.OrdinalIgnoreCase);

        //private readonly SortedDictionary<string, ProjectPropertyElement> _versionElements
        //    = new SortedDictionary<string, ProjectPropertyElement>(StringComparer.OrdinalIgnoreCase);

        private readonly ProjectRootElement _document;
        private ProjectPropertyGroupElement _autoPackageVersions;
        private ProjectPropertyGroupElement _pinnedPackageVersions;

        private DependencyVersionsFile(ProjectRootElement xDocument)
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

            var autoPackageVersions = projectRoot.AddPropertyGroup();
            autoPackageVersions.Label = AutoPackageVersionsLabel;

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

            var pinnedPackageVersions = projectRoot.AddPropertyGroup();
            pinnedPackageVersions.Label = PinnedPackageVersionsLabel;

            return new DependencyVersionsFile(projectRoot)
            {
                _autoPackageVersions = autoPackageVersions,
                _pinnedPackageVersions = pinnedPackageVersions,
            };
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

            var listPropertyGroups = new List<ProjectPropertyGroupElement>();

            foreach (var propGroup in propGroups)
            {
                var attr = propGroup.Label;
                if (attr != null && attr.StartsWith(PackageVersionsLabel, StringComparison.OrdinalIgnoreCase))
                {
                    file.HasVersionsPropertyGroup = true;
                    listPropertyGroups.Add(propGroup);
                }
            }

            foreach (var group in listPropertyGroups)
            {
                var isReadOnly = string.Equals(group.Label, PinnedPackageVersionsLabel, StringComparison.OrdinalIgnoreCase);
                if (isReadOnly)
                {
                    file._pinnedPackageVersions = group;
                }
                else
                {
                    file._autoPackageVersions = group;
                }

                foreach (var child in group.Properties)
                {
                    var variable = new PackageVersionVariable(child, isReadOnly);
                    file._versionVariables[variable.Name] = variable;
                }
            }

            file.EnsureGroupsCreated();
            return file;
        }

        private void EnsureGroupsCreated()
        {
            if (_autoPackageVersions == null)
            {
                _autoPackageVersions = _document.AddPropertyGroup();
                _autoPackageVersions.Label = AutoPackageVersionsLabel;
            }

            if (_pinnedPackageVersions == null)
            {
                _pinnedPackageVersions = _document.AddPropertyGroup();
                _pinnedPackageVersions.Label = PinnedPackageVersionsLabel;
            }
        }

        public static DependencyVersionsFile LoadFromProject(Project project)
        {
            var file = new DependencyVersionsFile(ProjectRootElement.Create(NewProjectFileOptions.None));

            foreach (var property in project.AllEvaluatedProperties.Where(p => p.Xml != null))
            {
                var group = (ProjectPropertyGroupElement)property.Xml.Parent;
                var isReadOnly = string.Equals(group.Label, PinnedPackageVersionsLabel, StringComparison.OrdinalIgnoreCase);

                var variable = new PackageVersionVariable(property.Xml, property.EvaluatedValue?.Trim(), isReadOnly);
                file._versionVariables[property.Name] = variable;
            }

            file.EnsureGroupsCreated();

            return file;
        }

        public bool HasVersionsPropertyGroup { get; private set; }

        // copying is required so calling .Set while iterating on this doesn't raise an InvalidOperationException
        public IReadOnlyDictionary<string, PackageVersionVariable> VersionVariables
            => new Dictionary<string, PackageVersionVariable>(_versionVariables, StringComparer.OrdinalIgnoreCase);

        public PackageVersionVariable Update(string variableName, string version)
        {
            if (!_versionVariables.TryGetValue(variableName, out var variable))
            {
                var element = _document.CreatePropertyElement(variableName);
                variable = new PackageVersionVariable(element, version, isReadOnly: false);
                _versionVariables[variableName] = variable;
                variable.AddToGroup(_autoPackageVersions);
            }

            variable.UpdateVersion(version);
            return variable;
        }

        public PackageVersionVariable AddPinnedVariable(string variableName, string version)
        {
            if (_versionVariables.ContainsKey(variableName))
            {
                throw new InvalidOperationException("Key already exists: " + variableName);
            }

            var element = _document.CreatePropertyElement(variableName);
            var variable = new PackageVersionVariable(element, version, isReadOnly: true);
            _versionVariables.Add(variableName, variable);
            variable.AddToGroup(_pinnedPackageVersions);
            return variable;
        }

        public void Save(string filePath)
        {
            _autoPackageVersions.RemoveAllChildren();
            foreach (var item in _versionVariables.Values.Where(v => !v.IsReadOnly).OrderBy(v => v.Name))
            {
                item.AddToGroup(_autoPackageVersions);
            }

            _document.Save(filePath, Encoding.UTF8);
        }
    }
}
