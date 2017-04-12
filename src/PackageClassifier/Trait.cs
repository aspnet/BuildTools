// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace PackageClassifier
{
    public class Trait : IEquatable<Trait>
    {
        public Trait(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public string Value { get; }

        public bool HasName(string name)
        {
            return string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);
        }

        public bool HasValue(string value)
        {
            return string.Equals(Value, value, StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(Trait other)
        {
            return other != null && other.HasName(Name) && other.HasValue(Value);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Trait);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Value.GetHashCode();
        }

        public override string ToString()
        {
            return $"Name '{Name}', Value '{Value}'";
        }
    }
}
