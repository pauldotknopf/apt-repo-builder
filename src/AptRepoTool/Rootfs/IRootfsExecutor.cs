namespace AptRepoTool.Rootfs
{
    public interface IRootfsExecutor
    {
        string MD5Sum { get; }
        
        void Build();
    }
}