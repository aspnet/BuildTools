// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.BuildTools
{
    /// <summary>
    /// <para>
    /// Generates a new file at <see cref="OutputPath"/>.
    /// </para>
    /// <para>
    /// The <see cref="TemplateFile"/> can define variables for substitution using <see cref="Properties"/>.
    /// </para>
    /// <example>
    /// The input file might look like this:
    /// <code>
    /// 2 + 2 = ${Sum}
    /// </code>
    /// When the task is invoked like this, it will produce "2 + 2 = 4"
    /// <code>
    /// &lt;GenerateFileFromTemplate Properties="Sum=4;OtherValue=123;" ... &gt;
    /// </code>
    /// </example>
    /// </summary>
#if SDK
    public class Sdk_GenerateFileFromTemplate : Task
#elif BuildTools
    public class GenerateFileFromTemplate : Task
#else
#error This must be built either for an SDK or for BuildTools
#endif
    {
        /// <summary>
        /// The template file.
        /// Variable syntax: ${VarName}
        /// If your template file needs to output this format, you can escape the dollar sign with a backtick, e.g. `${NotReplaced}
        /// </summary>
        [Required]
        public string TemplateFile { get; set; }

        /// <summary>
        /// The desitnation for the generated file.
        /// </summary>
        [Required]
        public string OutputPath { get; set; }

        /// <summary>
        /// Key=value pairs of values
        /// </summary>
        [Required]
        public string[] Properties { get; set; }

        /// <summary>
        /// The full path of the file written.
        /// </summary>
        [Output]
        public string FileWrites { get; set; }

        public override bool Execute()
        {
            var outputPath = Path.GetFullPath(OutputPath.Replace('\\', '/'));

            if (!File.Exists(TemplateFile))
            {
                Log.LogError("File {0} does not exist", TemplateFile);
                return false;
            }

            var values = MSBuildListSplitter.GetNamedProperties(Properties);
            var template = File.ReadAllText(TemplateFile);

            var result = Replace(template, values);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, result);

            FileWrites = Path.GetFullPath(outputPath);

            return true;
        }

        internal string Replace(string template, IDictionary<string, string> values)
        {
            var sb = new StringBuilder();
            var line = 1;
            for (var i = 0; i < template.Length; i++)
            {
                var ch = template[i];
                if (ch == '\n')
                {
                    line++;
                }

                if (ch == '`')
                {
                    i++;
                    if (i >= template.Length)
                    {
                        // slash ends doc
                        sb.Append(ch);
                        break;
                    }

                    ch = template[i];
                    if (ch != '$' && ch != '`')
                    {
                        // Not a known escape character
                        sb.Append('`');
                    }

                    sb.Append(ch);
                    continue;
                }

                if (ch != '$')
                {
                    sb.Append(ch);
                    continue;
                }

                i++;
                if (i >= template.Length)
                {
                    sb.Append('$');
                    break;
                }

                if (template[i] != '{')
                {
                    sb.Append('$').Append(template[i]);
                    continue;
                }

                var varNameSb = new StringBuilder();
                i++;
                for (; i < template.Length; i++)
                {
                    var nextCh = template[i];
                    if (nextCh != '}')
                    {
                        varNameSb.Append(nextCh);
                    }
                    else
                    {
                        var varName = varNameSb.ToString();
                        if (values.TryGetValue(varName, out var value))
                        {
                            sb.Append(value);
                        }
                        else
                        {
                            Log.LogWarning(null, null, null, TemplateFile,
                                line, 0, 0, 0,
                                message: "No property value is available for '{0}'",
                                messageArgs: new[] { varName });
                        }

                        varNameSb.Clear();
                        break;
                    }
                }

                if (varNameSb.Length > 0)
                {
                    Log.LogWarning(null, null, null, TemplateFile,
                                line, 0, 0, 0,
                                message: "Expected closing bracket for variable placeholder. No substitution will be made.");
                    sb.Append("${").Append(varNameSb.ToString());
                }
            }

            return sb.ToString();
        }
    }
}
