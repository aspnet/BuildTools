// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// Update or adds a NuGet feed to a NuGet.config file. It reads <see cref="NuGetConfigPath"/>
    /// and replaces or adds the feed named <see cref="SourceName"/> with <see cref="SourceUri'"/>.
    /// </summary>
    public class UpdatePackageSource : Task
    {
        [Required]
        public string NuGetConfigPath { get; set; }

        [Required]
        public string SourceName { get; set; }

        [Required]
        public string SourceUri { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(SourceName))
            {
                Log.LogError("FeedName must not be empty");
                return false;
            }

            if (string.IsNullOrEmpty(SourceUri))
            {
                Log.LogError("PackageSource must not be empty");
                return false;
            }

            var nugetConfig = XDocument.Load(NuGetConfigPath);
            var packageSources = nugetConfig.Element("configuration")?.Element("packageSources");
            var addElements = packageSources.Elements("add").ToList();

            var valueToUpdate = addElements.FirstOrDefault(f => string.Equals(f.Attribute("key")?.Value, SourceName, StringComparison.OrdinalIgnoreCase));
            if (valueToUpdate == null)
            {
                Log.LogMessage("Adding feed '{0}' to '{1}'", SourceName, SourceUri);
                packageSources.Add(new XElement("add",
                    new XAttribute("key", SourceName),
                    new XAttribute("value", SourceUri)));
            }
            else
            {
                Log.LogMessage("Updating feed '{0}' to '{1}'", SourceName, SourceUri);
                valueToUpdate.SetAttributeValue("value", SourceUri);
            }

            using (var file = new FileStream(NuGetConfigPath, FileMode.Create))
            {
                nugetConfig.Save(file);
            }

            Log.LogMessage("Saved changes to '{0}'", NuGetConfigPath);

            return true;
        }
    }
}
