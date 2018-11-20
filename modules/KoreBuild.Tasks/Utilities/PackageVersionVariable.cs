// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Build.Construction;

namespace KoreBuild.Tasks.Utilities
{
    public class PackageVersionVariable
    {
        private readonly ProjectPropertyElement _element;

        public PackageVersionVariable(ProjectPropertyElement element, bool isReadOnly)
            : this(element, element.Value?.Trim(), isReadOnly)
        {
        }

        public PackageVersionVariable(ProjectPropertyElement element, string version, bool isReadOnly)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
            IsReadOnly = isReadOnly;
            Name = element.Name.ToString();
            Version = version ?? string.Empty;
        }

        public string Name { get; }

        public string Version
        {
            get => _element.Value;
            private set => _element.Value = value;
        }

        public bool IsReadOnly { get; private set; }

        public void UpdateVersion(string version)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("You cannot updated a pinned package version variable automatically");
            }

            Version = version;
        }

        public void AddToGroup(ProjectPropertyGroupElement group)
        {
            group.AppendChild(_element);
        }

        public void SetLabel(string label)
        {
            _element.Label = label;
        }
    }
}
