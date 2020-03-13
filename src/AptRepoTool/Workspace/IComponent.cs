using System.Collections.ObjectModel;

namespace AptRepoTool.Workspace
{
    public interface IComponent
    {
        public string Name { get; }
        
        public ReadOnlyCollection<string> Dependencies { get; }
    }
}