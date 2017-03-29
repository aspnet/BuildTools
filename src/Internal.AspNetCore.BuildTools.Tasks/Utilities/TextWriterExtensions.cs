// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.BuildTools.Utilities;

namespace System.IO
{
    public static class TextWriterExtensions
    {
        public static IndentedTextWriter Indent(this TextWriter writer, int spaces = 4)
            => new IndentedTextWriter(writer, spaces);
    }
}
