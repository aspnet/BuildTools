// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;

namespace ApiCheck.Description
{
    [DebuggerDisplay("{" + nameof(Id) + ",nq}")]
    public class ApiElement
    {
        public virtual string Id { get; }
    }
}
