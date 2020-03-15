using System.IO;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AptRepoTool.Config.Impl
{
    public class ConfigParser : IConfigParser
    {
        public RootConfig LoadRootConfig(string yaml)
        {
            var json = ConvertToJson(yaml);

            return JsonConvert.DeserializeObject<RootConfig>(json);
        }

        public RootfsConfig LoadRootfsConfig(string yaml)
        {
            var json = ConvertToJson(yaml);
            var result = JsonConvert.DeserializeObject<RootfsConfig>(json);
            if (result.Type == "invalid")
            {
                throw new AptRepoToolException($"Invalid rootfs type.");
            }

            return result;
        }

        public ComponentConfig LoadComponentConfig(string yaml)
        {
            var json = ConvertToJson(yaml);
            return JsonConvert.DeserializeObject<ComponentConfig>(json);
        }

        private string ConvertToJson(string yaml)
        {
            using (var r = new StringReader(yaml))
            {
                var deserializer = new DeserializerBuilder().Build();
                var yamlObject = deserializer.Deserialize(r);
                var serializer = new SerializerBuilder()
                    .JsonCompatible()
                    .Build();

                return serializer.Serialize(yamlObject);
            }
        }

        class RawRootfs
        {
            public string Type { get; set; }
        }
    }
}