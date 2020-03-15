using JsonSubTypes;
using Newtonsoft.Json;

namespace AptRepoTool.Config
{
    [JsonConverter(typeof(JsonSubtypes), "Type")]
    [JsonSubtypes.KnownSubType(typeof(DockerRootfsConfig), "docker")]
    [JsonSubtypes.FallBackSubType(typeof(RootfsConfigInvalid))]
    public abstract class RootfsConfig
    {
        public abstract string Type { get; }
    }

    public class RootfsConfigInvalid : RootfsConfig
    {
        public override string Type => "invalid";
    }
    
    public class DockerRootfsConfig : RootfsConfig
    {
        public override string Type => "docker";
        
        public string Dockerfile { get; set; }
    }
}