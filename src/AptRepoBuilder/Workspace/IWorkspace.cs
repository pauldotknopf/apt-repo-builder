using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AptRepoBuilder.Workspace
{
    public interface IWorkspace
    {
        public string RootDirectory { get; }
        
        public ReadOnlyCollection<IComponent> Components { get; }

        public IComponent GetComponent(string name);

        public void BuildComponent(string name, ComponentBuildOptions options);

        public List<IComponent> GetComponentDependencies(string name);
        
        void BuildRootfs(bool force);

        void PublishRepo(string directory);

        void PublishCache();
        
        string CacheDirectory { get; }

        void AssertFixedCommits();
    }
}