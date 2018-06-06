// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.BuildTools.CodeSign
{
    /// <summary>
    /// A collection of sign request items.
    /// </summary>
    public class SignRequestCollection : IEnumerable<SignRequestItem>
    {
        private readonly SortedDictionary<string, SignRequestItem> _items = new SortedDictionary<string, SignRequestItem>(StringComparer.Ordinal);

        /// <inheritdoc />
        public IEnumerator<SignRequestItem> GetEnumerator() => _items.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.Values.GetEnumerator();

        /// <summary>
        /// The base path for files in the collection.
        /// </summary>
        public string BasePath { get; set; }

        /// <summary>
        /// Add an item to the collection.
        /// </summary>
        /// <param name="item">The item</param>
        public void Add(SignRequestItem item)
        {
            _items.Add(item.Path, item);
        }
    }
}
