// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace KoreBuild.Tasks.Tests
{
    public class BillOfMaterialsTests
    {
        [Fact]
        public void ArtifactIdsMustBeUnique()
        {
            var bom = new BillOfMaterials();
            bom.AddArtifact("abc", "t");
            Assert.Throws<ArgumentException>(() => bom.AddArtifact("abc", "t2"));
        }

        [Fact]
        public void DependencySourcesMustExist()
        {
            var bom = new BillOfMaterials();
            Assert.Throws<InvalidOperationException>(() => bom.Dependencies.AddLink("abc", "other"));
        }
    }
}
