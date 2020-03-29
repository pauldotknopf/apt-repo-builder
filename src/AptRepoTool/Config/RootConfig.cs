using System.Collections.Generic;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace AptRepoTool.Config
{
    public class RootConfig
    {
        public List<string> Components { get; set; }
        
        public RootRootfsConfig Rootfs { get; set; }

        public class RootRootfsConfig
        {
            public RootRootfsConfig()
            {
                RebuildComponentsOnChange = true;
            }
            
            public string Dir { get; set; }
            
            [JsonProperty("rebuild-components-on-change")]
            public bool RebuildComponentsOnChange { get; set; }
        }
    }
}