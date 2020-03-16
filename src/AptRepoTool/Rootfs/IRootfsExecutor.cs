using System.Collections.Generic;

namespace AptRepoTool.Rootfs
{
    public interface IRootfsExecutor
    {
        string MD5Sum { get; }
        
        void BuildRoot(bool force);
        
        void Run(string script, RunOptions options);
    }
}