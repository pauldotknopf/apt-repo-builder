using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AptRepoTool.Workspace.Impl
{
    public class Workspace : IWorkspace
    {
        private readonly List<IComponent> _components = new List<IComponent>();

        public Workspace(string rootDirectory)
        {
            RootDirectory = rootDirectory;
        }

        public string RootDirectory { get; }

        public ReadOnlyCollection<IComponent> Components => _components.AsReadOnly();

        public IComponent GetComponent(string name)
        {
            name.NotNullOrEmpty(nameof(name));
            
            var component = _components.SingleOrDefault(x => x.Name == name);
            if (component == null)
            {
                throw new AptRepoToolException($"No component found with the name {name.Quoted()}.");
            }

            return component;
        }
        
        public void AddComponent(IComponent component)
        {
            _components.Add(component);
        }
    }
}