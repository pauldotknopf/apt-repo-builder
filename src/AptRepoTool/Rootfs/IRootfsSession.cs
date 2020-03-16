using System;
using System.Collections.Generic;

namespace AptRepoTool.Rootfs
{
    public interface IRootfsSession : IDisposable
    {
        void Run(string command, Dictionary<string, string> env = null);
    }
}