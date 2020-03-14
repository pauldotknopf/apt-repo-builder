using System.Collections.Generic;
using System.Collections.ObjectModel;
using AptRepoTool.Git;
using Serilog;

namespace AptRepoTool.Workspace.Impl
{
    public class Component : IComponent
    {
        private readonly IGitCache _gitCache;

        public Component(string name,
            List<string> dependencies,
            string gitUrl,
            string branch,
            string revision,
            IGitCache gitCache)
        {
            _gitCache = gitCache;
            name.NotNullOrEmpty(nameof(name));
            gitUrl.NotNullOrEmpty(nameof(gitUrl));
            branch.NotNullOrEmpty(nameof(branch));
            revision.NotNullOrEmpty(nameof(revision));
            
            Name = name;
            Dependencies = (dependencies ?? new List<string>()).AsReadOnly();
            GitUrl = gitUrl;
            Branch = branch;
            if (revision == "latest")
            {
                SourceRev = new ComponentSrcRevLatest();
            }
            else
            {
                SourceRev = new ComponentSrcRevSpecific(revision);
            }
        }

        public string Name { get; }

        public ReadOnlyCollection<string> Dependencies { get; }
        
        public string GitUrl { get; }
        
        public string Branch { get; }

        public ComponentSrcRev SourceRev { get; private set; }
        
        public void ResolveUnknownCommit()
        {
            if (SourceRev.Type == ComponentSrcRevType.Latest)
            {
                Log.Information("Fetching latest commit for {component}...", Name);
                var commit = _gitCache.GetLatestCommit(GitUrl, Branch);
                SourceRev = new ComponentSrcRevSpecific(commit);
            }
        }

        public void FetchSources()
        {
            ResolveUnknownCommit();
            Log.Information("Fetching sources for {component}...", Name);
            _gitCache.Fetch(GitUrl);
            if (!_gitCache.ContainsBranchAndCommit(GitUrl, Branch, SourceRev.Commit))
            {
                throw new AptRepoToolException($"Commit {SourceRev.Commit.Quoted()} not found in branch {Branch.Quoted()} for component {Name.Quoted()}.");
            }
        }
    }
}