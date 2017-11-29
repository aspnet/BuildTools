// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace KoreBuild.Tasks.SkipStrongNames
{
    internal class AssemblySpecification
    {
        public string Name { get; set; }

        public string PublicKeyToken { get; set; }

        public override string ToString()
        {
            return Name + "," + PublicKeyToken;
        }
    }
}
