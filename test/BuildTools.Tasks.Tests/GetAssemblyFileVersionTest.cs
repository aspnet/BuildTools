// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.AspNetCore.BuildTools
{
    public class GetAssemblyFileVersionTest
    {
        [Fact]
        public void Execute_SetsAssemblyFileVersionToAssemblyVersion_IfRevisionIsSet()
        {
            // Arrange
            var getAssemblyFileVersion = new GetAssemblyFileVersion
            {
                AssemblyVersion = "1.2.3.4",
                AssemblyRevision = 78,
            };

            // Act
            var result = getAssemblyFileVersion.Execute();

            // Assert
            Assert.True(result);
            Assert.Equal("1.2.3.4", getAssemblyFileVersion.AssemblyFileVersion);
        }

        [Theory]
        [InlineData("1.2.3")]
        [InlineData("1.2.3.0")]
        public void Execute_UsesAssemblyRevision_IfRevisionIsNotSet(string assemblyVersion)
        {
            // Arrange
            var getAssemblyFileVersion = new GetAssemblyFileVersion
            {
                AssemblyVersion = assemblyVersion,
                AssemblyRevision = 78,
            };

            // Act
            var result = getAssemblyFileVersion.Execute();

            // Assert
            Assert.True(result);
            Assert.Equal("1.2.3.78", getAssemblyFileVersion.AssemblyFileVersion);
        }
    }
}
