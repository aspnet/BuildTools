// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace KoreBuild.Tasks
{
    internal abstract class SignRequestItem
    {
        public SignRequestItem(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public string Path { get; }

        public class Exclusion : SignRequestItem
        {
            public Exclusion(string path) : base(path)
            {
            }
        }

        public class File : SignRequestItem
        {
            public File(string path, string certificate, string strongName) : base(path)
            {
                Certificate = certificate;
                StrongName = strongName;
            }

            public string Certificate { get; }
            public string StrongName { get; }
        }

        public class Container : File
        {
            private readonly SignRequestCollection _items = new SignRequestCollection();

            public Container(string path, string type, string certificate, string strongName) : base(path, certificate, strongName)
            {
                Type = type ?? throw new ArgumentNullException(nameof(type));
            }

            public IEnumerable<SignRequestItem> Items => _items;

            public string Type { get; }

            public Container AddItem(SignRequestItem item)
            {
                _items.Add(item);
                return this;
            }
        }
    }
}
