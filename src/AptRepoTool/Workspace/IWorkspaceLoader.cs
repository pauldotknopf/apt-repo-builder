using System.Threading.Tasks;
using AptRepoTool.Models;
using AptRepoTool.Workspace.Impl;

namespace AptRepoTool.Workspace
{
    public interface IWorkspaceLoader
    {
        IWorkspace Load(string workspaceDirectory);
    }
}