// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using IOFile = System.IO.File;

namespace RepoTasks
{
    public class GenerateBadge : Task
    {
        private const string _template = @"
<svg xmlns=`http://www.w3.org/2000/svg` width=`200` height=`20`>
    <mask id=`a`>
        <rect width=`200` height=`20` rx=`0` fill=`#fff` />
    </mask>
    <g mask=`url(#a)`>
        <path fill=`#555` d=`M0 0h52v20H0z` />
        <path fill=`#007ec6` d=`M52 0h148v20H52z` />
        <path fill=`url(#b)` d=`M0 0h150v20H0z` />
    </g>
    <g fill=`#fff` text-anchor=`middle` font-family=`DejaVu Sans,Verdana,Geneva,sans-serif` font-size=`11`>
        <text x=`26` y=`15` fill=`#010101` fill-opacity=`.3`>version</text>
        <text x=`26` y=`14`>version</text>
        <text x=`125` y=`15` fill=`#010101` fill-opacity=`.3`>$</text>
        <text x=`125` y=`14`>$</text>
    </g>
</svg>";

        [Required]
        public string Version { get; set; }

        [Required]
        public string File { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrWhiteSpace(Version))
            {
                Log.LogError("Version cannot be an empty string");
                return false;
            }

            var sb = new StringBuilder();
            foreach (var ch in _template)
            {
                switch (ch)
                {
                    case '$':
                        sb.Append(Version);
                        break;
                    case '`':
                        sb.Append('"');
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(File));
            IOFile.WriteAllText(File, sb.ToString());
            return true;
        }
    }
}
