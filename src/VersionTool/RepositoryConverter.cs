using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VersionTool
{
    public class RepositoryConverter : JsonConverter
    {
        private readonly string _baseDirectoryPath;

        public RepositoryConverter(string baseDirectoryPath)
        {
            _baseDirectoryPath = baseDirectoryPath;
        }

        public override bool CanRead => true;

        public override bool CanWrite => true;

        public override bool CanConvert(Type objectType) => typeof(Repository).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var name = serializer.Deserialize<string>(reader);
            return new Repository(name, Path.Combine(_baseDirectoryPath, name));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var repository = value as Repository;

            if (repository != null)
            {
                writer.WriteValue(repository.Name);
            }
        }
    }
}
