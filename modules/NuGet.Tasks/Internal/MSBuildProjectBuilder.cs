// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Xml.Linq;

namespace NuGet.Tasks
{
    internal class MSBuildProjectBuilder
    {
        private readonly XDocument _document;

        public MSBuildProjectBuilder(string path)
        {
            if (!System.IO.Path.IsPathRooted(path))
            {
                throw new ArgumentException(nameof(path));
            }

            Path = path;
            _document = new XDocument();
            Project = new XElement("Project");
            _document.Add(Project);
        }

        public string Path { get; }

        public XElement Project { get; }

        public void Save()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            _document.Save(Path);
        }

        public XElement AddPropertyGroup()
        {
            var propertyGroup = new XElement("PropertyGroup");
            Project.Add(propertyGroup);
            return propertyGroup;
        }

        public XElement AddPropertyGroup(string name, string value)
        {
            var propertyGroup = AddPropertyGroup();
            propertyGroup.Add(new XElement(name, value));
            return propertyGroup;
        }

        public XElement AddImport(string path, bool required)
        {
            var import = new XElement("Import", new XAttribute("Project", path));
            if (!required)
            {
                import.Add(new XAttribute("Condition", $"Exists('{path}')"));
            }

            Project.Add(import);
            return import;
        }

        public XElement AddItemGroup()
        {
            var itemGroup = new XElement("ItemGroup");
            Project.Add(itemGroup);
            return itemGroup;
        }

        public XElement AddItemGroup(string condition)
        {
            var itemGroup = AddItemGroup();
            itemGroup.Add(new XAttribute("Condition", condition));
            return itemGroup;
        }

        public void AddToAllProjectsList()
        {
            AddPropertyGroup("MSBuildAllProjects",
                "$(MSBuildAllProjects);$(MSBuildThisFileFullPath)");
        }
    }
}
