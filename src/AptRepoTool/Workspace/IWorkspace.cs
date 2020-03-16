using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AptRepoTool.Workspace
{
    public interface IWorkspace
    {
        public string RootDirectory { get; }
        
        public ReadOnlyCollection<IComponent> Components { get; }

        public IComponent GetComponent(string name);

        public void BuildComponent(string name, ComponentBuildOptions options);
        
        void BuildRootfs(bool force);
    }
}