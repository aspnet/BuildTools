// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NuGet.Frameworks;
using KoreBuild.Tasks.ProjectModel;

namespace KoreBuild.Tasks
{
    internal class MSBuildProjectExtension
    {
        private readonly XDocument _document;
        private readonly XElement _defaultItemGroup;
        private readonly XElement _defaultPropertyGroup;
        private readonly Dictionary<NuGetFramework, XElement> _tfmGroups = new Dictionary<NuGetFramework, XElement>();

        public MSBuildProjectExtension(string path)
        {
            if (!System.IO.Path.IsPathRooted(path))
            {
                throw new ArgumentException(nameof(path));
            }

            Path = path;
            _document = new XDocument();
            Project = new XElement("Project");
            _document.Add(Project);

            // add this condition so when re-applying lineups we get the original references, not the pinned versions
            _defaultItemGroup = CreateRuntimeItemGroup();
            _defaultPropertyGroup = AddPropertyGroup();
            AddPropertyGroup("MSBuildAllProjects", "$(MSBuildAllProjects);$(MSBuildThisFileFullPath)");
        }

        public string Path { get; }

        public XElement Project { get; }

        public Dictionary<NuGetFramework, XElement> TfmGroups => _tfmGroups;

        public void Save()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            _document.Save(Path);
        }

        public XElement AddPropertyGroup(string name, string value)
        {
            var propertyGroup = AddPropertyGroup();
            propertyGroup.Add(new XElement(name, value));
            return propertyGroup;
        }

        public void AddImport(string path, bool required)
        {
            var import = new XElement("Import", new XAttribute("Project", path));
            if (!required)
            {
                import.Add(new XAttribute("Condition", $"Exists('{path}')"));
            }

            Project.Add(import);
        }

        public void AddAdditionalRestoreSource(string source)
        {
            _defaultPropertyGroup.Add(new XElement("RestoreAdditionalProjectSources", "$(RestoreAdditionalProjectSources);" + source));
        }

        public void AddLineup(string id, string version)
        {
            _defaultItemGroup.Add(new XElement("PackageLineup", new XAttribute("Include", id), new XAttribute("Version", version)));
        }

        public void PinPackageReference(string packageId, string version, NuGetFramework framework)
        {
            XElement itemGroup;
            if (framework.Equals(NuGetFramework.AnyFramework))
            {
                itemGroup = _defaultItemGroup;
            }
            else
            {
                if (!TfmGroups.TryGetValue(framework, out itemGroup))
                {
                    itemGroup = CreateRuntimeItemGroup($" AND '$(TargetFramework)' == '{framework.GetShortFolderName()}'");
                    TfmGroups.Add(framework, itemGroup);
                }
            }

            itemGroup.Add(CreatePinItem("PackageReference", packageId, version));
        }

        public void PinCliToolReference(DotNetCliReferenceInfo tool, string version)
        {
            _defaultItemGroup.Add(CreatePinItem("DotNetCliToolReference", tool.Id, version));
        }

        private XElement CreatePinItem(string type, string itemSpec, string version)
        {
            return new XElement(type,
                  new XAttribute("Update", itemSpec),
                  new XAttribute("Version", version),
                  // Prevents upgrades from the NuGet GUI in VS, and the version restriction policy can filter this reference.
                  new XAttribute("IsImplicitlyDefined", "true"));
        }

        private XElement CreateRuntimeItemGroup(string condition = null)
        {
            return AddItemGroup("'$(PolicyDesignTimeBuild)' != 'true' " + condition);
        }

        private XElement AddPropertyGroup()
        {
            var propertyGroup = new XElement("PropertyGroup");
            Project.Add(propertyGroup);
            return propertyGroup;
        }

        private XElement AddItemGroup()
        {
            var itemGroup = new XElement("ItemGroup");
            Project.Add(itemGroup);
            return itemGroup;
        }

        private XElement AddItemGroup(string condition)
        {
            var itemGroup = AddItemGroup();
            itemGroup.Add(new XAttribute("Condition", condition));
            return itemGroup;
        }
    }
}
