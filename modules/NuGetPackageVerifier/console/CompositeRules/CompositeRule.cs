// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetPackageVerifier
{
    public abstract class CompositeRule : IPackageVerifierRule
    {
        protected abstract IPackageVerifierRule[] Rules { get; }

        public IEnumerable<IPackageVerifierRule> GetRules()
        {
            var rules = new List<IPackageVerifierRule>();
            foreach (var rule in Rules)
            {
                if (rule is CompositeRule compositeRule)
                {
                    rules.AddRange(compositeRule.GetRules());
                }
                else
                {
                    rules.Add(rule);
                }
            }

            return rules;
        }

        public virtual IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            foreach (var rule in Rules)
            {
                foreach (var issue in rule.Validate(context))
                {
                    yield return issue;
                }
            }
        }
    }
}
