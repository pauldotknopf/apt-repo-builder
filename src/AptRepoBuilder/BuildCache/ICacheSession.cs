using System;

namespace AptRepoBuilder.BuildCache
{
    public interface ICacheSession : IDisposable
    {
        string Dir { get; }
        
        void Commit();
    }
}