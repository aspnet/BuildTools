// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace ApiCheck.Description
{
    public class ApiListing
    {
        public string AssemblyIdentity { get; set; }
        public IList<TypeDescriptor> Types { get; } = new List<TypeDescriptor>();

        public TypeDescriptor FindType(string id)
        {
            foreach (var type in Types)
            {
                if (string.Equals(id, type.Id, StringComparison.Ordinal))
                {
                    return type;
                }
            }

            return null;
        }
    }
}
