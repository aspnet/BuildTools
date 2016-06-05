// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PackageClassifier
{
    public class Classification
    {
        private readonly string[] _traits;

        public Classification(string[] traits)
        {
            _traits = traits;
        }

        public IList<ClassificationEntry> Entries { get; } = new List<ClassificationEntry>();

        public string Diagnostics { get; private set; }

        public void AddClassifiedElement(string pattern, Trait[] traits)
        {
            if (_traits.Length != traits.Length ||
                _traits.Any(t => !traits.Any(et => et.HasName(t))))
            {
                throw new ArgumentException("Pattern is missing a trait.");
            }

            if (Entries.Any(e => e.HasIdentity(pattern)))
            {
                throw new ArgumentException("Duplicate patterns are not allowed.");
            }

            Entries.Add(new ClassificationEntry(pattern, traits));
        }

        public static Classification FromCsv(TextReader reader)
        {
            // Package,Category,OptimizedCache
            // Microsoft.AspNetCore.Razor.Tools,ship,exclude
            // Microsoft.VisualStudio.Web.CodeGeneration.Tools,ship,exclude
            // Microsoft.Extensions.SecretManager.Tools,ship,exclude

            var lines = new List<string>();
            var line = reader.ReadLine();

            while (line != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
                line = reader.ReadLine();
            }

            var traits = lines
                .First()
                .Split(',')
                .Skip(1)
                .Select(s => s.Trim())
                .ToArray();

            var entries = lines
                .Skip(1)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(',').Select(w => w.Trim()).ToArray())
                .ToArray();

            var invalidEntries = new List<int>();

            var classification = new Classification(traits);

            // Order the entries so that most specific entries come first.
            entries = entries.OrderByDescending(l => l[0], StringComparer.OrdinalIgnoreCase).ToArray();

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.Length == traits.Length + 1)
                {
                    classification.AddClassifiedElement(entry[0], traits.Select((t, j) => new Trait(t, entry[j + 1])).ToArray());
                }
                else
                {
                    invalidEntries.Add(i);
                }
            }

            if (invalidEntries.Count > 0)
            {
                var messageItems = string.Concat(invalidEntries.Select(i => $"{Environment.NewLine}    Line {i}: {string.Join(", ", entries[i])}"));
                classification.Diagnostics = $"The following entries are invalid:{messageItems}";
            }

            return classification;
        }
    }
}
