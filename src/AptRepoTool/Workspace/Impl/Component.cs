using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using AptRepoTool.BuildCache;
using AptRepoTool.Git;
using AptRepoTool.Shell;
using Serilog;

namespace AptRepoTool.Workspace.Impl
{
    public class Component : IComponent
    {
        private readonly IGitCache _gitCache;
        private readonly Workspace _workspace;
        private readonly IBuildCache _buildCache;
        private readonly IShellRunner _shellRunner;

        public Component(string name,
            List<string> dependencies,
            string gitUrl,
            string branch,
            string revision,
            IGitCache gitCache,
            Workspace workspace,
            IBuildCache buildCache,
            IShellRunner shellRunner)
        {
            _gitCache = gitCache;
            _workspace = workspace;
            _buildCache = buildCache;
            _shellRunner = shellRunner;
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

        public string MD5 { get; private set; } = null;
        
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

        public void Extract(string directory)
        {
            _gitCache.Extract(GitUrl, Branch, SourceRev.Commit, directory);
        }

        public void CalculateMD5Sum()
        {
            if (!string.IsNullOrEmpty(MD5))
            {
                return;
            }

            if (SourceRev.Type == ComponentSrcRevType.Latest)
            {
                ResolveUnknownCommit();
            }
            
            var hash = $"{SourceRev.Commit}{GitUrl}";
            foreach (var dependency in Dependencies)
            {
                var component = _workspace.GetComponent(dependency);
                component.CalculateMD5Sum();
                hash += component.MD5;
            }

            MD5 = hash.CalculateMD5Hash();
        }
        
        public void Build(bool force)
        {
            Log.Information("Building {component}...", Name);
            
            // Ensure all commits are resolved, and all dependencies have calculated their MD5.
            CalculateMD5Sum();

            if (_buildCache.HasCacheDirectory($"packages-{Name}-{MD5}"))
            {
                // This component was already built, and it's outputs are available.
                if (force)
                {
                    Log.Warning("Forcing a rebuild of {component}...", Name);
                }
                else
                {
                    Log.Information("Skipping {component}, it is already built.", Name);
                    return;
                }
            }
            
            // Make sure we have all the sources.
            FetchSources();

            var buildDirectory = _buildCache.StartSession($"build-{Name}", false);
            
            // Prepare the git directory
            var gitDirectory = Path.Combine(buildDirectory.Dir, "git");
            gitDirectory.CleanOrCreateDirectory();
            
            // Checkout the code.
            Extract(gitDirectory);
            
            // Prepare the packages directory
            var packagesDirectory = Path.Combine(buildDirectory.Dir, "packages");
            packagesDirectory.CleanOrCreateDirectory();
            
            // Pretend to build
            // TODO: Build for real
            File.WriteAllText(Path.Combine(packagesDirectory, $"{Name}.deb"), "");

            using (var packagesCache = _buildCache.StartSession($"packages-{Name}-{MD5}", true))
            {
                _shellRunner.RunShell($"cp -r {Path.Combine(packagesDirectory, "*")} .", new RunnerOptions
                {
                    WorkingDirectory = packagesCache.Dir
                });
                packagesCache.Commit();
            }
        }
    }
}