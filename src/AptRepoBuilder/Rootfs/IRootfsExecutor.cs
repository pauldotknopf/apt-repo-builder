namespace AptRepoBuilder.Rootfs
{
    public interface IRootfsExecutor
    {
        string MD5Sum { get; }
        
        void BuildRoot(bool force);

        void CheckCache(string directory);
        
        void PublishCache(string directory);

        IRootfsSession StartSession(RunOptions options);
    }
}