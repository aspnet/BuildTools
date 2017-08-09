// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Tasks.ProjectModel;

namespace NuGet.Tasks.Policies
{
    internal class MSBuildLineupFileBuilder
    {
        private readonly MSBuildProjectBuilder _builder;
        private readonly XElement _defaultItemGroup;
        private readonly Dictionary<NuGetFramework, XElement> _tfmGroups = new Dictionary<NuGetFramework, XElement>();

        public MSBuildLineupFileBuilder(MSBuildProjectBuilder builder)
        {
            _builder = builder;
            // add this condition so when re-applying lineups we get the original references, not the pinned versions
            _defaultItemGroup = CreateRuntimeItemGroup();
        }

        public void AddLineup(string id, string version)
        {
            _defaultItemGroup.Add(new XElement("PackageLineup", new XAttribute("Include", id), new XAttribute("Version", version)));
        }

        public void PinPackageReference(PackageReferenceInfo package, string version, NuGetFramework framework)
        {
            XElement itemGroup;
            if (framework.Equals(NuGetFramework.AnyFramework))
            {
                itemGroup = _defaultItemGroup;
            }
            else
            {
                if (!_tfmGroups.TryGetValue(framework, out itemGroup))
                {
                    itemGroup = CreateRuntimeItemGroup($" AND '$(TargetFramework)' == '{framework.GetShortFolderName()}'");
                    _tfmGroups.Add(framework, itemGroup);
                }
            }

            itemGroup.Add(CreatePinItem("PackageReference", package.Id, version));
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
                  new XAttribute("AutoVersion", "true"),
                  // Prevents upgrades from the NuGet GUI in VS, and the NoVersions policy can filter this reference.
                  new XAttribute("IsImplicitlyDefined", "true"));
        }

        private XElement CreateRuntimeItemGroup(string condition = null)
        {
            return _builder.AddItemGroup("'$(PolicyDesignTimeBuild)' != 'true' " + condition);
        }
    }
}
