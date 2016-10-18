// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using ApiCheck.Description;

namespace ApiCheck
{
    public static class ApiListingFilters
    {
        public static bool NonExportedMembers(MemberInfo e)
        {
            var t = e as TypeInfo;
            if (t != null)
            {
                return !(t.IsPublic || t.IsNestedPublic || t.IsNestedFamily);
            }

            var m = e as MethodBase;
            if (m != null)
            {
                return !(m.IsPublic || m.IsFamily);
            }

            var f = e as FieldInfo;
            if (f != null)
            {
                return !(f.IsPublic || f.IsFamily);
            }

            return false;
        }

        public static bool NonExportedMembers(ApiElement e)
        {
            var t = e as TypeDescriptor;
            if (t != null)
            {
                return t.Visibility != ApiElementVisibility.Public &&
                    t.Visibility != ApiElementVisibility.Protected &&
                    t.Visibility != ApiElementVisibility.ProtectedInternal;
            }

            var m = e as MemberDescriptor;
            if (m != null)
            {
                return m.Visibility != ApiElementVisibility.Public &&
                    m.Visibility != ApiElementVisibility.Protected &&
                    m.Visibility != ApiElementVisibility.ProtectedInternal;
            }

            return false;
        }

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
