namespace AptRepoTool.Git
{
    public class GitCacheOptions
    {
        public GitCacheOptions()
        {
            GitCacheDir = "/tmp/apt-repo-tool-git-cache";
        }
        
        public string GitCacheDir { get; set; }
    }
}