// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace ApiCheck.NuGet
{
    public class RuntimeDefinition
    {
        public string Name { get; set; }
        public IEnumerable<RuntimeDefinition> Fallbacks { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
