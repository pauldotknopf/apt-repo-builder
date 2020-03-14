namespace AptRepoTool.Git
{
    public interface IGitCache
    {
        void Fetch(string url);

        bool ContainsBranchAndCommit(string url, string branch, string commit);
        
        string GetLatestCommit(string url, string branch);
    }
}