using System.Collections.ObjectModel;

namespace AptRepoTool.Workspace.Impl
{
    public interface IWorkspace
    {
        public string RootDirectory { get; }
        
        public ReadOnlyCollection<IComponent> Components { get; }

        public IComponent GetComponent(string name);
    }
}