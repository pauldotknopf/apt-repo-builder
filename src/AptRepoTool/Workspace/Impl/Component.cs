using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AptRepoTool.Workspace.Impl
{
    public class Component : IComponent
    {
        public Component(string name, List<string> dependencies)
        {
            Name = name;
            Dependencies = (dependencies ?? new List<string>()).AsReadOnly();
        }

        public string Name { get; }

        public ReadOnlyCollection<string> Dependencies { get; }
    }
}