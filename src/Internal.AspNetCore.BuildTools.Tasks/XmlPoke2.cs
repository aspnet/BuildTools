// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

// TODO remove when XmlPoke is restored to MSBuild 15.2
namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// Similar behavior as MSBuild's XmlPoke task, but for .NET Core.
    /// </summary>
    public class XmlPoke2 : Task
    {
        [Required]
        public ITaskItem XmlInputPath { get; set; }

        [Required]
        public string Query { get; set; }

        [Required]
        public ITaskItem Value { get; set; }

        public override bool Execute()
        {
            var xmlDoc = new XmlDocument();
            var xrs = new XmlReaderSettings()
            {
                DtdProcessing = DtdProcessing.Ignore
            };

            using (var sr = XmlReader.Create(XmlInputPath.ItemSpec, xrs))
            {
                xmlDoc.Load(sr);
            }

            var nav = xmlDoc.CreateNavigator();
            var expr = nav.Compile(Query);
            var iter = nav.Select(expr);
            while (iter.MoveNext())
            {
                iter.Current.InnerXml = Value.ItemSpec;
            }

            if (iter.Count > 0)
            {
                using (var file = new FileStream(XmlInputPath.ItemSpec, FileMode.Create))
                {
                    Log.LogMessage(MessageImportance.Normal, "Updating {0}", XmlInputPath.ItemSpec);
                    xmlDoc.Save(file);
                }
            }

            return true;
        }
    }
}
