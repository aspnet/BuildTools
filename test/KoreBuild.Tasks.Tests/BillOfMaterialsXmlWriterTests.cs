// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    public class BillOfMaterialsXmlWriterTests
    {
        private readonly ITestOutputHelper _output;

        public BillOfMaterialsXmlWriterTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ItSerializesBomToXml()
        {
            var bom = new BillOfMaterials();
            var test = bom.AddArtifact("Test", "NuGetPackage").SetMetadata("TargetFramework", "netstandard2.0");
            var ship = bom.AddArtifact("Shipping", "NuGetPackage");
            ship.Category = "Ship";

            bom.Dependencies.AddLink(test, ship);

            var sb = new StringBuilder();
            var writer = new StringWriter(sb);
            using (var xml = new BillOfMaterialsXmlWriter(writer))
            {
                xml.Write(bom);
            }

            var expected = $@"<Build>
  <Artifacts>
    <Artifact Id=`Test` Type=`NuGetPackage` TargetFramework=`netstandard2.0` />
  </Artifacts>
  <Artifacts Category=`Ship`>
    <Artifact Id=`Shipping` Type=`NuGetPackage` />
  </Artifacts>
  <Dependencies>
    <Link Source=`Test` Target=`Shipping` />
  </Dependencies>
</Build>".Replace('`', '"');

            _output.WriteLine(sb.ToString());
            Assert.Equal(expected, sb.ToString(), ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
        }
    }
}
