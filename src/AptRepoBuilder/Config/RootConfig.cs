using System.Collections.Generic;
using Newtonsoft.Json;

namespace AptRepoBuilder.Config
{
    public class RootConfig
    {
        public List<string> Components { get; set; }
        
        public string Cache { get; set; }
        
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