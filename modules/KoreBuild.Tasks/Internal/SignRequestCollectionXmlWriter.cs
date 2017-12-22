// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace KoreBuild.Tasks
{
    internal class SignRequestCollectionXmlWriter : IDisposable
    {
        private readonly TextWriter output;
        private readonly XDocument document;

        public SignRequestCollectionXmlWriter(TextWriter output)
        {
            this.output = output;
            document = new XDocument(new XElement("SignRequests"));
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

        public void Write(SignRequestCollection signRequestCollection)
        {
            var node = document.Root;
            foreach (var request in signRequestCollection)
            {
                AddRequest(node, request);
            }
        }

        private static void AddRequest(XElement parent, SignRequestItem item)
        {
            var path = new XAttribute("Path", item.Path);
            switch (item)
            {
                case SignRequestItem.Container c:
                    var container = new XElement("Container",
                        path,
                        new XAttribute("Type", c.Type));

                    if (!string.IsNullOrEmpty(c.Certificate))
                    {
                        container.Add(new XAttribute("Certificate", c.Certificate));
                    }

                    if (!string.IsNullOrEmpty(c.StrongName))
                    {
                        container.Add(new XAttribute("StrongName", c.StrongName));
                    }

                    parent.Add(container);

                    foreach (var i in c.Items)
                    {
                        AddRequest(container, i);
                    }

                    break;
                case SignRequestItem.Exclusion e:
                    parent.Add(new XElement("ExcludedFile", path));
                    break;
                case SignRequestItem.File f:
                    var file = new XElement("File", path);

                    if (!string.IsNullOrEmpty(f.Certificate))
                    {
                        file.Add(new XAttribute("Certificate", f.Certificate));
                    }

                    if (!string.IsNullOrEmpty(f.StrongName))
                    {
                        file.Add(new XAttribute("StrongName", f.StrongName));
                    }

                    parent.Add(file);
                    break;
                    throw new InvalidOperationException("Unrecognized sign request item");
            }
        }

        public void Dispose()
        {
            Save();
        }
    }
}
