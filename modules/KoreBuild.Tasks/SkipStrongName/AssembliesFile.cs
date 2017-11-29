// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace KoreBuild.Tasks.SkipStrongNames
{
    internal static class AssembliesFile
    {
        public static AssemblySpecification[] Read(string path)
        {
            XElement assembliesElement = XElement.Load(path);

            if (assembliesElement.Name != "assemblies")
            {
                throw new InvalidOperationException("The name of the root element must be assemblies.");
            }

            XAttribute defaultPublicKeyTokenAttribute = assembliesElement.Attribute("defaultPublicKeyToken");

            string defaultPublicKeyToken;

            if (defaultPublicKeyTokenAttribute != null)
            {
                defaultPublicKeyToken = defaultPublicKeyTokenAttribute.Value;
            }
            else
            {
                defaultPublicKeyToken = null;
            }

            List<AssemblySpecification> specifications = new List<AssemblySpecification>();

            foreach (XElement assembly in assembliesElement.Elements("assembly"))
            {
                XAttribute nameAttribute = assembly.Attribute("name");

                if (nameAttribute == null)
                {
                    throw new InvalidOperationException("An assembly element must have a name attribute.");
                }

                XAttribute publicKeyTokenAttribute = assembly.Attribute("publicKeyToken");
                string publicKeyToken;

                if (publicKeyTokenAttribute != null)
                {
                    publicKeyToken = publicKeyTokenAttribute.Value;
                }
                else
                {
                    publicKeyToken = defaultPublicKeyToken;
                }

                if (publicKeyToken == null)
                {
                    throw new InvalidOperationException("An assembly element must have a publicKeyToken attribute, " +
                        "or the assemblies element must have a defaultPublicKeyToken attribute.");
                }

                specifications.Add(new AssemblySpecification
                {
                    Name = nameAttribute.Value,
                    PublicKeyToken = publicKeyToken
                });
            }

            if (specifications.Count == 0)
            {
                throw new InvalidOperationException(
                    "The assemblies element must contain at least one assembly element.");
            }

            return specifications.ToArray();
        }
    }
}
