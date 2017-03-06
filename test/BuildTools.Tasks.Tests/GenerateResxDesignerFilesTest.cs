// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.AspNetCore.BuildTools;
using Microsoft.Build.Utilities;
using Xunit;

namespace BuildTools.Tasks.Tests
{
    public class GenerateResxDesignerFilesTest
    {
        [Fact]
        public void GeneratesResx()
        {
            var resx = Path.Combine(AppContext.BaseDirectory, "Resources", "Strings.resx");

            var item = new TaskItem(resx);
            item.SetMetadata("ManifestResourceName", "Microsoft.Extensions.Logging.Abstractions.Resource");
            item.SetMetadata("Type", "Resx");

            var engine = new MockEngine();
            var task = new GenerateResxDesignerFiles
            {
                ResourceFiles = new[] { item },
                BuildEngine = engine,
            };

            var expectedFile = Path.Combine(AppContext.BaseDirectory, "Resources", "Strings.Designer.cs.txt");
            var actualFile = Path.Combine(AppContext.BaseDirectory, "Resources", "Strings.Designer.cs");
            if (File.Exists(actualFile))
            {
                File.Delete(actualFile);
            }

            Assert.True(task.Execute(), "Task failed");
            Assert.Empty(engine.Warnings);

            Assert.Equal(actualFile, Assert.Single(task.FileWrites).ItemSpec);
            Assert.True(File.Exists(actualFile), "Actual file does not exist");
            Assert.Equal(File.ReadAllText(expectedFile), File.ReadAllText(actualFile), ignoreLineEndingDifferences: true);
        }
    }
}
