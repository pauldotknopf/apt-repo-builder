using System.Collections.Generic;

namespace AptRepoTool.Rootfs
{
    public class RunOptions
    {
        public RunOptions()
        {
            Mounts = new List<MountedVolume>();
        }
        
        public List<MountedVolume> Mounts { get; set; }
    }
}