using System.IO;
using JsonSubTypes;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace AptRepoBuilder.Config.Impl
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
            
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(JsonSubtypesConverterBuilder
                .Of(typeof(RootfsConfig), "Type") // type property is only defined here
                .RegisterSubtype(typeof(DockerRootfsConfig), "docker")
                .RegisterSubtype(typeof(TarballRootfsConfig), "tarball")
                .SetFallbackSubtype(typeof(UnknownRootfsConfig))
                .SerializeDiscriminatorProperty() // ask to serialize the type property
                .Build());
            
            var result = JsonConvert.DeserializeObject<RootfsConfig>(json, settings);
            if (result == null)
            {
                throw new AptRepoToolException($"Invalid {"rootfs".Quoted()} value.");
            }
            if (result.Type == "invalid")
            {
                throw new AptRepoToolException($"Invalid {"rootfs".Quoted()} value.");
            }

            return result;
        }

        public ComponentConfig LoadComponentConfig(string yaml)
        {
            var json = ConvertToJson(yaml);
            
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(JsonSubtypesConverterBuilder
                .Of(typeof(ComponentConfig.Step), "Type") // type property is only defined here
                .RegisterSubtype(typeof(ComponentConfig.MakeOrigStep), "make-orig")
                .RegisterSubtype(typeof(ComponentConfig.SourceBuildStep), "source-build")
                .RegisterSubtype(typeof(ComponentConfig.BashStep), "bash")
                .SetFallbackSubtype(typeof(UnknownStep))
                .SerializeDiscriminatorProperty() // ask to serialize the type property
                .Build());
            
            var result = JsonConvert.DeserializeObject<ComponentConfig>(json, settings);
            
            if (result.Steps != null)
            {
                foreach (var step in result.Steps)
                {
                    if (step.Type == "invalid")
                    {
                        throw new AptRepoToolException($"Invalid {"type".Quoted()} for step.");
                    }
                }
            }

            return result;
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

        private class UnknownRootfsConfig : RootfsConfig
        {
            public override string Type => "invalid";
        }

        private class UnknownStep : ComponentConfig.Step
        {
            public override string Type => "invalid";
        }
    }
}