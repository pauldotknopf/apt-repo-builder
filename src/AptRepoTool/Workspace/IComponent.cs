using System.Collections.ObjectModel;

namespace AptRepoTool.Workspace
{
    public interface IComponent
    {
        public string Name { get; }
        
        public string MD5 { get; }
        
        public ReadOnlyCollection<string> Dependencies { get; }
        
        public string GitUrl { get; }
        
        public string Branch { get; }
        
        public ComponentSrcRev SourceRev { get; }

        void ResolveUnknownCommit();

        void FetchSources();

        void Extract(string directory);
        
        void CalculateMD5Sum();

        void Build(bool force);
    }
}