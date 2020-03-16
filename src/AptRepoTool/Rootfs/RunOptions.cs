using System.Collections.Generic;

namespace AptRepoTool.Rootfs
{
    public class RunOptions
    {
        public RunOptions()
        {
            Mounts = new List<MountedVolume>();
            Env = new Dictionary<string, string>();
        }
        
        public bool Interactive { get; set; }
        
        public List<MountedVolume> Mounts { get; }
        
        public Dictionary<string, string> Env { get; }
    }
}