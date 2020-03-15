using System.Collections.Generic;

namespace AptRepoTool.Config
{
    public class ComponentConfig
    {
        public string Url { get; set; }
            
        public string Branch { get; set; }
            
        public string Commit { get; set; }
            
        public List<string> Dependencies { get; set; }
    }
}