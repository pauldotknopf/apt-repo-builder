using System.Collections.Generic;

namespace AptRepoTool.Config
{
    public class RootConfig
    {
        public List<string> Components { get; set; }
        
        public string Rootfs { get; set; }
    }
}