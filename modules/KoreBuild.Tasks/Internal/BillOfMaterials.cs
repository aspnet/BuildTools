// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace KoreBuild.Tasks
{
    internal class BillOfMaterials
    {
        private readonly Dictionary<string, Artifact> _artifacts = new Dictionary<string, Artifact>(StringComparer.OrdinalIgnoreCase);

        public BillOfMaterials()
        {
            Dependencies = new DependencyGraph(this);
        }

        public string Id { get; set; }

        public IReadOnlyDictionary<string, Artifact> Artifacts => _artifacts;

        public DependencyGraph Dependencies { get; }

        public Artifact AddArtifact(string id, string type)
        {
            var artifact = new Artifact(id, type);
            _artifacts.Add(id, artifact);
            return artifact;
        }

        public class DependencyGraph
        {
            public DependencyGraph(BillOfMaterials parent)
            {
                _parent = parent;
            }

            private readonly List<Link> _links = new List<Link>();
            private readonly BillOfMaterials _parent;

            public IReadOnlyList<Link> Links => _links;

            public void AddLink(Artifact source, Artifact target) => AddLink(source.Id, target.Id);

            public void AddLink(string source, string target) => AddLink(new Link(source, target));

            public void AddLink(Link link)
            {
                if (!_parent.Artifacts.ContainsKey(link.Source))
                {
                    throw new InvalidOperationException($"A dependency cannot be added for {link.Source} because it does not exist in {nameof(BillOfMaterials.Artifacts)}.");
                }
                _links.Add(link);
            }
        }

        public class Link
        {
            public Link(string source, string target)
            {
                if (string.IsNullOrEmpty(source))
                {
                    throw new ArgumentException("message", nameof(source));
                }

                if (string.IsNullOrEmpty(target))
                {
                    throw new ArgumentException("message", nameof(target));
                }

                Source = source;
                Target = target;
            }

            public string Source { get; }
            public string Target { get; }
        }

        public class Artifact
        {
            private readonly Dictionary<string, string> _metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public Artifact(string id, string type)
            {
                if (string.IsNullOrEmpty(id))
                {
                    throw new ArgumentException(nameof(id));
                }

                if (string.IsNullOrEmpty(type))
                {
                    throw new ArgumentException(nameof(type));
                }

                Id = id;
                Type = type;
            }

            public string Id { get; }
            public string Type { get; }
            public string Category { get; set; }

            public IReadOnlyDictionary<string, string> Metadata => _metadata;

            public Artifact SetMetadata(string name, string value)
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new ArgumentException(nameof(name));
                }

                if (IsReservedMetadataName(name))
                {
                    throw new ArgumentException("Name cannot use reserved metadata names: Id, Type, Category", nameof(name));
                }

                _metadata[name] = value;

                return this;
            }

            public bool IsReservedMetadataName(string name)
            {
                return name.Equals("Id", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Type", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Category", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
