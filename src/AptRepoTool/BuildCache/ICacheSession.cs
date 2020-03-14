using System;

namespace AptRepoTool.BuildCache
{
    public interface ICacheSession : IDisposable
    {
        string Dir { get; }
        
        void Commit();

        void Clean();
    }
}