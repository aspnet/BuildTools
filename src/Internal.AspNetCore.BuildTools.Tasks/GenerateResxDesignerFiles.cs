// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// Generates C# for resx files. It is expected to run on all EmbeddedResource items named *.resx and after
    /// the 'PrepareResourceNames' target.
    /// <seealso cref="Microsoft.Build.Tasks.CreateManifestResourceName"/>.
    /// </summary>
    public class GenerateResxDesignerFiles : Task
    {
        /// <summary>
        /// Expected metadata items on <see cref="ResourceFiles"/>.
        /// </summary>
        private static class Metadata
        {
            public const string GeneratedFileName = "GeneratedFileName";
            public const string ManifestResourceName = "ManifestResourceName";
            public const string Type = "Type";
            public const string FullPath = "FullPath";
            public const string WithCulture = "WithCulture";
        }

        private static Regex _namedParameterMatcher = new Regex(@"\{([a-z]\w+)\}", RegexOptions.IgnoreCase);
        private static Regex _numberParameterMatcher = new Regex(@"\{(\d+)\}");
        private List<ITaskItem> _createdFiles = new List<ITaskItem>();

        /// <summary>
        /// <para>
        /// The resx files to be generated.
        /// </para>
        /// <para>
        /// Metadata: 'GeneratedFileName' can be used to set where the C# is written. Defaults to creating a file in the same folder.
        /// Metadata: 'ManifestResourceName' created by the CreateManifestResourceNames target.
        /// </para>
        /// </summary>
        [Required]
        public ITaskItem[] ResourceFiles { get; set; }

        /// <summary>
        /// An item for each generated file.
        /// </summary>
        [Output]
        public ITaskItem[] FileWrites { get; set; }

        public override bool Execute()
        {
            foreach (var item in ResourceFiles)
            {
                var resourceName = item.GetMetadata(Metadata.ManifestResourceName);
                if (string.IsNullOrEmpty(resourceName))
                {
                    Log.LogWarning("'{0}' was not set on {1}.", Metadata.ManifestResourceName, item.ItemSpec);
                    continue;
                }

                if (!"Resx".Equals(item.GetMetadata(Metadata.Type), StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogMessage(MessageImportance.High, "Skipping resource '{0}' with type '{1}", item.ItemSpec, item.GetMetadata(Metadata.Type));
                    continue;
                }

                if ("true".Equals(item.GetMetadata(Metadata.WithCulture), StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogMessage(MessageImportance.High, "Skipping culture-specific resource '{0}'", item.ItemSpec);
                    continue;
                }

                try
                {
                    var resxFile = item.GetMetadata(Metadata.FullPath) ?? item.ItemSpec;
                    GenerateCsharp(
                        resxFile,
                        item.GetMetadata(Metadata.GeneratedFileName),
                        resourceName);
                }
                catch (IOException ex)
                {
                    Log.LogError("Failed to generated C# for {0}:\n{1}", item.ItemSpec, ex.ToString());
                    return false;
                }
            }

            FileWrites = _createdFiles.ToArray();

            return true;
        }

        private bool GenerateCsharp(string resxFile, string outputFileName, string manifestName)
        {
            if (string.IsNullOrEmpty(outputFileName))
            {
                outputFileName = Path.GetFileNameWithoutExtension(resxFile) + ".Designer.cs";
            }

            var outputFullPath = !Path.IsPathRooted(outputFileName)
                ? Path.Combine(Path.GetDirectoryName(resxFile), outputFileName)
                : outputFileName;

            // normalize separator
            outputFullPath = Path.DirectorySeparatorChar == '\\'
                ? outputFullPath.Replace('/', Path.DirectorySeparatorChar)
                : outputFullPath.Replace('\\', Path.DirectorySeparatorChar);

            var fileName = Path.GetFileNameWithoutExtension(resxFile).Replace(".", "_");
            var resourceStrings = new List<ResourceData>();
            if (!File.Exists(resxFile))
            {
                Log.LogError("'{0}' does not exist", resxFile);
                return false;
            }

            var xml = XDocument.Load(resxFile);

            Log.LogMessage(MessageImportance.Low, "Used '{0}' to generate '{1}'", resxFile, outputFileName);
            Log.LogMessage(MessageImportance.High, "Generated {0}", outputFullPath);

            foreach (var entry in xml.Descendants("data"))
            {
                var name = entry.Attribute("name").Value;
                var value = entry.Element("value").Value;

                bool usingNamedArgs = true;
                var match = _namedParameterMatcher.Matches(value);
                if (match.Count == 0)
                {
                    usingNamedArgs = false;
                    match = _numberParameterMatcher.Matches(value);
                }

                var arguments = match.Cast<Match>()
                                     .Select(m => m.Groups[1].Value)
                                     .Distinct();
                if (!usingNamedArgs)
                {
                    arguments = arguments.OrderBy(Convert.ToInt32);
                }

                resourceStrings.Add(
                    new ResourceData
                    {
                        Name = name,
                        Value = value,
                        Arguments = arguments.ToList(),
                        UsingNamedArgs = usingNamedArgs
                    });
            }

            var resourceNamespace = manifestName.Substring(0, manifestName.LastIndexOf('.'));
            var resourceTypeName = manifestName.Substring(manifestName.LastIndexOf('.') + 1);

            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath));

            using (var stream = new FileStream(outputFullPath, FileMode.Create))
            using (var writer = new StreamWriter(stream))
            {
                _createdFiles.Add(new TaskItem(outputFullPath));

                writer.WriteLine(
        $@"// <auto-generated />
namespace {resourceNamespace}
{{
    using System.Globalization;
    using System.Reflection;
    using System.Resources;

    internal static class {resourceTypeName}
    {{
        private static readonly ResourceManager _resourceManager
            = new ResourceManager(""{manifestName}"", typeof({resourceTypeName}).GetTypeInfo().Assembly);");

                using (var indent = writer.Indent(8))
                {
                    foreach (var resourceString in resourceStrings)
                    {
                        writer.WriteLine();
                        RenderHeader(indent, resourceString);
                        RenderProperty(indent, resourceString);

                        writer.WriteLine();
                        RenderHeader(indent, resourceString);
                        RenderFormatMethod(indent, resourceString);
                    }
                }

                writer.WriteLine(@"
        private static string GetString(string name, params string[] formatterNames)
        {
            var value = _resourceManager.GetString(name);

            System.Diagnostics.Debug.Assert(value != null);

            if (formatterNames != null)
            {
                for (var i = 0; i < formatterNames.Length; i++)
                {
                    value = value.Replace(""{"" + formatterNames[i] + ""}"", ""{"" + i + ""}"");
                }
            }

            return value;
        }
    }
}");

            }

            return true;
        }


        private static void RenderHeader(TextWriter writer, ResourceData resourceString)
        {
            writer.WriteLine("/// <summary>");
            foreach (var line in resourceString.Value.Split(new[] { '\n' }, StringSplitOptions.None))
            {
                writer.WriteLine($"/// {new XText(line)}");
            }
            writer.WriteLine("/// </summary>");
        }

        private static void RenderProperty(TextWriter writer, ResourceData resourceString)
        {
            writer.WriteLine("internal static string {0}", resourceString.Name);
            writer.WriteLine("{");
            using (var indent = writer.Indent(4))
            {
                indent.WriteLine($@"get => GetString(""{resourceString.Name}"");");
            }
            writer.WriteLine("}");
        }

        private static void RenderFormatMethod(TextWriter writer, ResourceData resourceString)
        {
            writer.WriteLine($"internal static string Format{resourceString.Name}({resourceString.Parameters})");

            using (var indent = writer.Indent(4))
            {
                if (resourceString.Arguments.Count > 0)
                {
                    indent.WriteLine(@"=> string.Format(CultureInfo.CurrentCulture, GetString(""{0}""{1}), {2});",
                                resourceString.Name,
                                resourceString.UsingNamedArgs ? ", " + resourceString.FormatArguments : null,
                                resourceString.ArgumentNames);
                }
                else
                {
                    indent.WriteLine($@"=> GetString(""{resourceString.Name}"");");
                }
            }
        }

        private class ResourceData
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public List<string> Arguments { get; set; }

            public bool UsingNamedArgs { get; set; }

            public string FormatArguments
            {
                get { return string.Join(", ", Arguments.Select(a => "\"" + a + "\"")); }
            }

            public string ArgumentNames
            {
                get { return string.Join(", ", Arguments.Select(GetArgName)); }
            }

            public string Parameters
            {
                get { return string.Join(", ", Arguments.Select(a => "object " + GetArgName(a))); }
            }

            public string GetArgName(string name)
            {
                return UsingNamedArgs ? name : 'p' + name;
            }
        }
    }
}
