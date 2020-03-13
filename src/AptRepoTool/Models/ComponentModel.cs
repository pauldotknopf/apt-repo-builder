using System.Collections.Generic;

namespace AptRepoTool.Models
{
    public class ComponentModel
    {
        public string GitUrl { get; set; }
        
        public string Branch { get; set; }
        
        public string SrcRev { get; set; }
        
        public List<string> Dependencies { get; set; }
    }
}