// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.BuildTools.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace KoreBuild.Tasks
{
    public class MergeXmlFiles : Task
    {
        [Required]
        public string OutputPath { get; set; }

        [Required]
        public ITaskItem[] Files { get; set; }

        public override bool Execute()
        {
            if (Files == null || Files.Length == 0)
            {
                Log.LogError("No files could be found to merge");
                return false;
            }

            OutputPath = FileHelpers.NormalizePath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            if (Files.Length == 1)
            {
                File.Copy(Files[0].ItemSpec, OutputPath);
                return true;
            }

            return Execute(() => File.CreateText(OutputPath));
        }

        protected internal bool Execute(Func<TextWriter> writerFactory)
        {
            var newDoc = XDocument.Load(Files[0].ItemSpec, LoadOptions.PreserveWhitespace);

            for (var i = 1; i < Files.Length; i++)
            {
                var next = XDocument.Load(Files[i].ItemSpec, LoadOptions.PreserveWhitespace);
                if (newDoc.Root.Name != next.Root.Name)
                {
                    Log.LogError($"Can only merge documents with the same root element. {Files[0].ItemSpec} has <{newDoc.Root.Name}> but {Files[i].ItemSpec} has <{next.Root.Name}>");
                    continue;
                }

                newDoc.Root.Add(next.Root.DescendantNodes());

                foreach (var attr in next.Root.Attributes())
                {
                    newDoc.Root.SetAttributeValue(attr.Name, attr.Value);
                }
            }

            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Encoding = Encoding.UTF8,
            };

            using (var writer = writerFactory())
            using (var xmlWriter = XmlWriter.Create(writer, settings))
            {
                newDoc.Save(xmlWriter);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
