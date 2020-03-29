using System;
using System.Collections.Generic;

namespace AptRepoBuilder.Rootfs
{
    public interface IRootfsSession : IDisposable
    {
        void Run(string command, Dictionary<string, string> env = null);
    }
}