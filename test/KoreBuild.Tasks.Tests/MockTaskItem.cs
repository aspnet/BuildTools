// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace KoreBuild.Tasks.Tests
{
    internal class MockTaskItem : ITaskItem
    {
        private Dictionary<string, string> _metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public MockTaskItem(string itemSpec)
        {
            ItemSpec = itemSpec;
        }

        public string this[string index]
        {
            get => GetMetadata(index);
            set => SetMetadata(index, value);
        }

        public string ItemSpec { get; set; }

        public ICollection MetadataNames => _metadata.Keys;

        public int MetadataCount => _metadata.Count;

        public IDictionary CloneCustomMetadata()
        {
            return new Dictionary<string, string>(_metadata);
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            destinationItem.ItemSpec = ItemSpec;
            foreach (var item in _metadata)
            {
                destinationItem.SetMetadata(item.Key, item.Value);
            }
        }

        public string GetMetadata(string metadataName)
        {
            _metadata.TryGetValue(metadataName, out var retVal);
            return retVal;
        }

        public void RemoveMetadata(string metadataName)
        {
            if (_metadata.ContainsKey(metadataName))
            {
                _metadata.Remove(metadataName);
            }
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            _metadata[metadataName] = metadataValue;
        }
    }
}
