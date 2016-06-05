// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PackageClassifier
{
    public class Classifier
    {
        public Classifier(PackageSourcesCache packages, Classification classification)
        {
            Cache = packages;
            Classification = classification;

            MapPackages();
        }

        public IList<PackageMapping> PackageMappings { get; } = new List<PackageMapping>();

        public IList<string> Diagnostics { get; } = new List<string>();

        public PackageSourcesCache Cache { get; }

        public Classification Classification { get; }

        public ClassificationResult GetClassification(string trait)
        {
            var classificationResult = new ClassificationResult(trait);

            foreach (var mapping in PackageMappings)
            {
                var mappingTrait = mapping.Entry.GetTrait(trait);
                classificationResult.AddPackages(mappingTrait, mapping.Packages);
            }

            return classificationResult;
        }

        private void MapPackages()
        {
            foreach (var entry in Classification.Entries)
            {
                PackageMappings.Add(new PackageMapping(entry, Cache.GetById(entry.Identity)));
            }

            DetectDuplicatePackagesAcrossFolders();
            DetectMissingPackagesOnClassification();
            DetectMissingPackagesOnPackageSourcesCache();
        }

        private void DetectMissingPackagesOnClassification()
        {
            var notClassified = Cache.Packages
                .Where(p => !PackageMappings.Any(m => m.HasPackage(p)));

            if (notClassified.Any())
            {
                var formattedPackagesWithoutClassification = FormatWithIndent(notClassified.Select(p => p.FullPath).ToArray());
                var diagnostic = $"No entries in the classification matched the following packages: {formattedPackagesWithoutClassification}";

                Diagnostics.Add(diagnostic);
            }
        }

        private void DetectDuplicatePackagesAcrossFolders()
        {
            var duplicates = Cache.Packages
                .GroupBy(p => p.Identity)
                .Where(g => g.Count() > 1);

            var duplicateEntries = duplicates
                .Select(g => $"The package with id '{g.Key}' is duplicated:{FormatWithIndent(g.Select(p => p.FullPath).ToArray())}");

            foreach (var duplicate in duplicateEntries)
            {
                Diagnostics.Add(duplicate);
            }
        }

        private void DetectMissingPackagesOnPackageSourcesCache()
        {
            var missingOnSource = PackageMappings
                .Where(m => m.Packages.Length == 0);

            if (missingOnSource.Any())
            {
                var formattedMissingPackagesOnSource = FormatWithIndent(missingOnSource.Select(mos => mos.Entry.Identity).ToArray());
                var formattedDiagnostic = $"No packages found for the following patterns: {formattedMissingPackagesOnSource}";

                Diagnostics.Add(formattedDiagnostic);
            }
        }

        private string FormatWithIndent(params string[] lines)
        {
            return string.Concat(lines.Select(l => $"{Environment.NewLine}    {l}"));
        }
    }
}
