// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;

namespace Microsoft.AspNetCore.BuildTools.Utilities
{
    public class IndentedTextWriter : TextWriter, IDisposable
    {
        private readonly TextWriter _wrapped;
        private readonly string _spaces;

        public IndentedTextWriter(TextWriter wrapped, int indentSpaces)
        {
            _wrapped = wrapped;
            _spaces = new String(' ', indentSpaces);
        }

        public override Encoding Encoding => _wrapped.Encoding;

        public override void Write(char value)
            => _wrapped.Write(value);

        public override void WriteLine(string line)
        {
            _wrapped.Write(_spaces);
            _wrapped.WriteLine(line);
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
