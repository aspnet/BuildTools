// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using ApiCheck.Description;

namespace ApiCheck
{
    public static class ApiListingFilters
    {
        public static bool InternalNamespaceTypes(MemberInfo e)
        {
            var type = e as TypeInfo;
            if (type == null)
            {
                return false;

            }

            return type.Namespace.EndsWith(".Internal");
        }

        public static bool InternalNamespaceTypes(ApiElement e)
        {
            var type = e as TypeDescriptor;
            if (type == null)
            {
                return false;
            }

            var segments = type.Name.Split('.');
            return segments.Length > 1 && segments[segments.Length - 2].Equals("Internal");
        }
    }
}
