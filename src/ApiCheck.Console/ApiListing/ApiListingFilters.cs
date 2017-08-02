// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Reflection;
using ApiCheck.Description;

namespace ApiCheck
{
    public static class ApiListingFilters
    {
        public static bool IsInInternalNamespace(MemberInfo e)
        {
            var type = e as TypeInfo;
            if (type == null)
            {
                return false;
            }

            var segments = type.Namespace.Split('.');
            return segments.Any(s => s == "Internal");
        }

        public static bool IsInInternalNamespace(ApiElement e)
        {
            var type = e as TypeDescriptor;
            if (type == null)
            {
                return false;
            }

            var segments = type.Name.Split('.');
            // Skip the last segment as is the type name.
            return segments.Take(segments.Length - 1).Any(s => s.Equals("Internal"));
        }
    }
}
