// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.AspNetCore.BuildTools.CodeSign
{
    /// <summary>
    /// Reads a sign request manifest xml fil.
    /// </summary>
    public class SignRequestManifestXmlReader
    {
        private readonly TextReader _reader;
        private readonly string _manifestBasePath;

        /// <summary>
        /// Load the sign request xml file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static SignRequestCollection Load(string filePath)
        {
            using (var reader = File.OpenText(filePath))
            {
                return Load(reader, Path.GetDirectoryName(filePath));
            }
        }

        /// <summary>
        /// Read the sign request xml using a given base path.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="manifestBasePath"></param>
        /// <returns></returns>
        public static SignRequestCollection Load(TextReader reader, string manifestBasePath)
        {
            return new SignRequestManifestXmlReader(reader, manifestBasePath).Read();
        }

        private SignRequestManifestXmlReader(TextReader reader, string manifestBasePath)
        {
            _reader = reader;
            _manifestBasePath = manifestBasePath;
        }

        private SignRequestCollection Read()
        {
            var doc = XDocument.Load(_reader);

            var collection = new SignRequestCollection
            {
                BasePath = _manifestBasePath
            };

            foreach (var element in doc.Root.Elements())
            {
                collection.Add(ReadElement(element));
            }

            return collection;
        }

        private SignRequestItem ReadElement(XElement element)
        {
            switch (element.Name.ToString())
            {
                case "Nupkg":
                    return ReadNupkg(element);
                case "Vsix":
                    return ReadVsix(element);
                case "Zip":
                    return ReadZip(element);
                case "File":
                    return SignRequestItem.CreateFile(GetPath(element), GetCertificate(element), GetStrongName(element));
                case "ExcludedFile":
                    return SignRequestItem.CreateExclusion(GetPath(element));
                default:
                    IXmlLineInfo lineInfo = element;
                    throw new InvalidDataException($"Unrecognized element type {element.Name} on line {lineInfo.LineNumber}");
            }
        }

        private SignRequestItem ReadNupkg(XElement element)
        {
            var package = SignRequestItem.CreateNugetPackage(GetPath(element), GetCertificate(element));
            AddChildren(element, package);
            return package;
        }

        private SignRequestItem ReadVsix(XElement element)
        {
            var vsix = SignRequestItem.CreateVsix(GetPath(element), GetCertificate(element));
            AddChildren(element, vsix);
            return vsix;
        }

        private SignRequestItem ReadZip(XElement element)
        {
            var zip = SignRequestItem.CreateZip(GetPath(element));
            AddChildren(element, zip);
            return zip;
        }

        private void AddChildren(XElement element, SignRequestItem package)
        {
            foreach (var child in element.Elements())
            {
                package.AddChild(ReadElement(child));
            }
        }

        private static string GetStrongName(XElement element) => element.Attribute("StrongName")?.Value;
        private static string GetCertificate(XElement element) => element.Attribute("Certificate")?.Value;
        private static string GetPath(XElement element) => element.Attribute("Path")?.Value;
    }
}
