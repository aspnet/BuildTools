// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace KoreBuild.Tasks
{
    internal class HashHelper
    {
        public static string GetFileHash(string algorithmName, string filePath)
        {
            byte[] hash;
            using (var stream = File.OpenRead(filePath))
            {
                HashAlgorithm algorithm;
                switch (algorithmName.ToUpperInvariant())
                {
                    case "SHA256":
                        algorithm = new SHA256Managed();
                        break;
                    case "SHA384":
                        algorithm = new SHA384Managed();
                        break;
                    case "SHA512":
                        algorithm = new SHA512Managed();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Unsupported hash algoritm {algorithmName}", nameof(algorithm));
                }
                hash = algorithm.ComputeHash(stream);
            }

            var sb = new StringBuilder();
            foreach (var b in hash)
            {
                sb.AppendFormat("{0:X2}", b);
            }

            return sb.ToString();
        }
    }
}
