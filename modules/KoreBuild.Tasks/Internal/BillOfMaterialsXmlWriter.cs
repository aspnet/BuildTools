// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace KoreBuild.Tasks
{
    internal class BillOfMaterialsXmlWriter : IDisposable
    {
        private readonly TextWriter output;
        private readonly XDocument document;

        public BillOfMaterialsXmlWriter(TextWriter output)
        {
            this.output = output;
            document = new XDocument(new XElement("Build"));
        }

        public void Save()
        {
            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace,
            };

            using (var writer = XmlWriter.Create(output, settings))
            {
                document.Save(writer);
            }
        }

        public void Write(BillOfMaterials bom)
        {
            if (!string.IsNullOrEmpty(bom.Id))
            {
                document.Root.Add(new XAttribute("Id", bom.Id));
            }


            AddArtifacts(bom);
            AddDependencies(bom);
        }

        private void AddDependencies(BillOfMaterials bom)
        {
            if (bom.Dependencies.Links.Count == 0)
            {
                return;
            }

            var dependencyGroup = new XElement("Dependencies");
            document.Root.Add(dependencyGroup);

            foreach (var link in bom.Dependencies.Links.OrderBy(l => l.Source + l.Target, StringComparer.Ordinal))
            {
                dependencyGroup.Add(new XElement("Link", new XAttribute("Source", link.Source), new XAttribute("Target", link.Target)));
            }
        }

        private void AddArtifacts(BillOfMaterials bom)
        {
            if (bom.Artifacts.Count == 0)
            {
                return;
            }

            foreach (var artifactGroup in bom.Artifacts.Values.GroupBy(a => a.Category))
            {
                var groupElem = new XElement("Artifacts");
                document.Root.Add(groupElem);
                if (artifactGroup.Key != null)
                {
                    groupElem.Add(new XAttribute("Category", artifactGroup.Key));
                }

                foreach (var artifact in artifactGroup.OrderBy(a => a.Id, StringComparer.Ordinal))
                {
                    var elem = new XElement("Artifact", new XAttribute("Id", artifact.Id), new XAttribute("Type", artifact.Type));
                    groupElem.Add(elem);

                    foreach (var item in artifact.Metadata)
                    {
                        elem.Add(new XAttribute(item.Key, item.Value));
                    }
                }
            }
        }

        public void Dispose()
        {
            Save();
        }
    }
}
