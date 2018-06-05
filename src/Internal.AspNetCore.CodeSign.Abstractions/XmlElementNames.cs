// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Xml.Linq;

namespace Microsoft.AspNetCore.BuildTools.CodeSign
{
    internal static class XmlElementNames
    {
        public static readonly string StrongName = "StrongName";
        public static readonly string Certificate = "Certificate";
        public static readonly string Path = "Path";
        public static readonly string File = "File";
        public static readonly string ExcludedFile = "ExcludedFile";
        public static readonly string Vsix = "Vsix";
        public static readonly string Nupkg = "Nupkg";
        public static readonly string Zip = "Zip";
    }
}
