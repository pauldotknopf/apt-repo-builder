using System.Collections.Generic;

namespace AptRepoBuilder.Rootfs
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