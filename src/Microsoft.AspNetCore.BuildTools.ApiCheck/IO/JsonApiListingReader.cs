// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ApiCheck.Description;
using Newtonsoft.Json;

namespace ApiCheck.IO
{
    public class JsonApiListingReader: IApiListingReader
    {
        private readonly IEnumerable<Func<ApiElement, bool>> _filters;
        private readonly JsonReader _json;

        public JsonApiListingReader(TextReader reader, IEnumerable<Func<ApiElement, bool>> filters = null)
        {
            _json = new JsonTextReader(reader);
            _filters = filters ?? Enumerable.Empty<Func<ApiElement, bool>>();
        }

        public ApiListing Read()
        {
            var serializer = new JsonSerializer();
            var listing = serializer.Deserialize<ApiListing>(_json);

            foreach (var type in listing.Types.ToArray())
            {
                if (_filters.Any(filter => filter(type)))
                {
                    listing.Types.Remove(type);
                }

                foreach (var member in type.Members.ToArray())
                {
                    if (_filters.Any(filter => filter(member)))
                    {
                        type.Members.Remove(member);
                    }
                }
            }
            return listing;
        }

        public void Dispose()
        {
            (_json as IDisposable)?.Dispose();
        }
    }
}
