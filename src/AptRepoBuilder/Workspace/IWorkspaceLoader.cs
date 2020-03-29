namespace AptRepoBuilder.Workspace
{
    public interface IWorkspaceLoader
    {
        IWorkspace Load(string workspaceDirectory);
    }
}