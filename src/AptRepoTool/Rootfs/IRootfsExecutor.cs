namespace AptRepoTool.Rootfs
{
    public interface IRootfsExecutor
    {
        void Configure(string rootfsDirectory);
        
        string MD5Sum { get; }
        
        void Build();
    }
}