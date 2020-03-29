using System.Collections.ObjectModel;

namespace AptRepoBuilder.Workspace
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

        void ExtractSource(string directory);

        void ExtractPackages(string directory);

        string GetPackagesDirectory();
        
        void CalculateMD5Sum();

        void Build(bool force, bool bashPrompt);
    }
}