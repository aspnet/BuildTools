// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using ApiCheck.Description;

namespace ApiCheck
{
    public class BreakingChange
    {
        public BreakingChange(ApiElement oldItem, string context = null)
        {
            Context = context;
            Item = oldItem;
        }
        public string Context { get; }

        public ApiElement Item { get; }

        public override string ToString() => Context == null ? Item.Id : $"{Context} => {Item.Id}";
    }
}
