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
    /// Generates an SVG file badge that can be embedded into a markdown page
    /// </summary>
#if SDK
    public class Sdk_GenerateSvgBadge : Microsoft.Build.Utilities.Task
    {
#elif BuildTools
    public class GenerateSvgBadge : Microsoft.Build.Utilities.Task
    {
#else
#error This must be built either for an SDK or for BuildTools
#endif

        private static readonly string Template = @"
<svg xmlns=`http://www.w3.org/2000/svg` width=`200` height=`20`>
    <mask id=`a`>
        <rect width=`200` height=`20` rx=`0` fill=`#fff` />
    </mask>
    <g mask=`url(#a)`>
        <path fill=`#555` d=`M0 0h52v20H0z` />
        <path fill=`${Color}` d=`M52 0h148v20H52z` />
        <path fill=`url(#b)` d=`M0 0h150v20H0z` />
    </g>
    <g fill=`#fff` text-anchor=`middle` font-family=`DejaVu Sans,Verdana,Geneva,sans-serif` font-size=`11`>
        <text x=`26` y=`15` fill=`#010101` fill-opacity=`.3`>${Label}</text>
        <text x=`26` y=`14`>${Label}</text>
        <text x=`125` y=`15` fill=`#010101` fill-opacity=`.3`>${Value}</text>
        <text x=`125` y=`14`>${Value}</text>
    </g>
</svg>".Replace('`', '"');

        /// <summary>
        /// The value that appears on the left side of the badge.
        /// </summary>
        [Required]
        public string Label { get; set; } = "version";

        /// <summary>
        /// The value that appears on the right side of the badge.
        /// </summary>
        [Required]
        public string Value { get; set; }

        private string Color { get; set; } = "#007EC6";

        [Required]
        public string OutputPath { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(Label))
            {
                Log.LogError("Label cannot be an empty string");
                return false;
            }

            if (string.IsNullOrEmpty(Value))
            {
                Log.LogError("Value cannot be an empty string");
                return false;
            }

            if (string.IsNullOrEmpty(Color))
            {
                Log.LogError("Color cannot be an empty string");
                return false;
            }
#if SDK
            var generator = new Sdk_GenerateFileFromTemplate()
#else
            var generator = new GenerateFileFromTemplate()
#endif
            {
                BuildEngine = BuildEngine,
                OutputPath = OutputPath,
                HostObject = HostObject,
                TemplateFile = "<badge template>",
            };

            var result = generator.Replace(Template, new Dictionary<string, string>
            {
                ["Label"] = Label,
                ["Value"] = Value,
                ["Color"] = Color,
            });

            var outputPath = Path.GetFullPath(OutputPath.Replace('\\', '/'));

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, result);

            return true;
        }
    }
}
