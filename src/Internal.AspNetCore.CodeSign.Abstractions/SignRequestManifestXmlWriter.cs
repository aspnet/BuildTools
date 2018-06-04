// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.AspNetCore.BuildTools.CodeSign
{
    /// <summary>
    /// Saves a <see cref="SignRequestCollection" /> to an xml file.
    /// </summary>
    public class SignRequestManifestXmlWriter : IDisposable
    {
        private readonly TextWriter _output;
        private readonly XDocument _document;

        /// <summary>
        /// Initalize an instance of <see cref="SignRequestManifestXmlWriter"/>
        /// </summary>
        /// <param name="output"></param>
        public SignRequestManifestXmlWriter(TextWriter output)
        {
            _output = output;
            _document = new XDocument(new XElement("SignRequest"));
        }

        /// <summary>
        /// Add the collection to the output file
        /// </summary>
        /// <param name="signRequestCollection"></param>
        public void Write(SignRequestCollection signRequestCollection)
        {
            var node = _document.Root;
            foreach (var request in signRequestCollection)
            {
                AddRequest(node, request);
            }
        }

        private static void AddRequest(XElement parent, SignRequestItem item)
        {
            var path = new XAttribute("Path", item.Path);
            switch (item.ItemType)
            {
                case SignRequestItemType.Zip:
                    AddContainer(parent, item, "Zip");
                    break;
                case SignRequestItemType.Nupkg:
                    AddContainer(parent, item, "Nupkg");
                    break;
                case SignRequestItemType.Vsix:
                    AddContainer(parent, item, "Vsix");
                    break;
                case SignRequestItemType.Exclusion:
                    parent.Add(new XElement("ExcludedFile", path));
                    break;
                case SignRequestItemType.File:
                    var file = new XElement("File", path);

                    if (!string.IsNullOrEmpty(item.Certificate))
                    {
                        file.Add(new XAttribute("Certificate", item.Certificate));
                    }

                    if (!string.IsNullOrEmpty(item.StrongName))
                    {
                        file.Add(new XAttribute("StrongName", item.StrongName));
                    }

                    parent.Add(file);
                    break;
                default:
                    throw new InvalidOperationException("Unrecognized sign request item");
            }
        }

        private static void AddContainer(XElement parent, SignRequestItem item, string elementType)
        {
            var container = new XElement(elementType, new XAttribute("Path", item.Path));

            if (!string.IsNullOrEmpty(item.Certificate))
            {
                container.Add(new XAttribute("Certificate", item.Certificate));
            }

            parent.Add(container);

            foreach (var i in item.Children)
            {
                AddRequest(container, i);
            }
        }

        private void Save()
        {
            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace,
            };

            using (var writer = XmlWriter.Create(_output, settings))
            {
                _document.Save(writer);
            }
        }

        /// <inheritdoc />
        public void Dispose() => Save();
    }
}
