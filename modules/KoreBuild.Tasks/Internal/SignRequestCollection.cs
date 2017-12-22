// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace KoreBuild.Tasks
{
    internal class SignRequestCollection : IEnumerable<SignRequestItem>
    {
        private SortedDictionary<string, SignRequestItem> _items = new SortedDictionary<string, SignRequestItem>(StringComparer.Ordinal);

        public IEnumerator<SignRequestItem> GetEnumerator() => _items.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.Values.GetEnumerator();

        public void Add(SignRequestItem item)
        {
            _items.Add(item.Path, item);
        }
    }
}
