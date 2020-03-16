using JsonSubTypes;
using Newtonsoft.Json;

namespace AptRepoTool.Config
{
    public abstract class RootfsConfig
    {
        public abstract string Type { get; }
    }

    public class DockerRootfsConfig : RootfsConfig
    {
        public override string Type => "docker";
        
        public string Dockerfile { get; set; }
    }

    public class TarballRootfsConfig : RootfsConfig
    {
        public override string Type => "tarball";
    }
}