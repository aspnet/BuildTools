// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using ApiCheck.Description;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ApiCheck.IO
{
    public class JsonApiListingWriter : IApiListingWriter
    {
        private readonly string _filePath;

        public JsonApiListingWriter(string filePath)
        {
            _filePath = filePath;
        }

        public void Write(ApiListing listing)
        {
            using (var writer = new JsonTextWriter(File.CreateText(_filePath)))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 2;
                writer.IndentChar = ' ';

                JObject.FromObject(listing).WriteTo(writer);
            }
        }
    }
}
