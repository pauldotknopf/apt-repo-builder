using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using AptRepoBuilder.Apt;
using AptRepoBuilder.Rootfs;
using Serilog;

namespace AptRepoBuilder.Workspace.Impl
{
    public class Workspace : IWorkspace
    {
        private readonly string _cacheDirectory;
        private readonly IRootfsExecutor _rootfsExecutor;
        private readonly IAptHelper _aptHelper;
        private List<IComponent> _components = new List<IComponent>();

        public Workspace(string rootDirectory,
            string cacheDirectory,
            IRootfsExecutor rootfsExecutor,
            IAptHelper aptHelper)
        {
            _cacheDirectory = cacheDirectory;
            _rootfsExecutor = rootfsExecutor;
            _aptHelper = aptHelper;
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

        public void BuildComponent(string name, ComponentBuildOptions options)
        {
            if (options == null)
            {
                options = new ComponentBuildOptions();
            }

            BuildRootfs(false);

            // Build all the dependencies
            foreach (var dependency in GetComponentDependencies(name))
            {
                dependency.Build(options.ForceBuildDependencies, options.PromptBeforeBuild);
            }
            
            // Build the target component
            GetComponent(name).Build(options.ForceBuild, options.PromptBeforeBuild);
        }

        public List<IComponent> GetComponentDependencies(string name)
        {
            var sorted = new List<IComponent>();
            var visited = new Dictionary<string, bool>();

            void Visit(IComponent component)
            {
                var alreadyVisited = visited.TryGetValue(component.Name, out var inProcess);

                if (alreadyVisited)
                {
                    if (inProcess)
                    {
                        throw new ArgumentException("Cyclic dependency found.");
                    }
                }
                else
                {
                    visited[component.Name] = true;

                    foreach (var dependency in component.Dependencies)
                    {
                        Visit(GetComponent(dependency));
                    }
                    
                    visited[component.Name] = false;
                    sorted.Add(component);
                }
            }
            
            Visit(GetComponent(name));

            return sorted.Where(x => x.Name != name).ToList();
        }

        public void BuildRootfs(bool force)
        {
            if (!string.IsNullOrEmpty(_cacheDirectory))
            {
                _rootfsExecutor.CheckCache(_cacheDirectory);
            }
            _rootfsExecutor.BuildRoot(force);
        }

        public void PublishRepo(string directory)
        {
            Log.Information("Cleaning {directory}...", directory);
            directory.CleanOrCreateDirectory();

            Log.Information("Extracting all the components...");
            
            foreach (var component in _components)
            {
                component.ExtractPackages(directory);
            }
            
            Log.Information("Indexing all source/binary packages...");
            _aptHelper.ScanSourcesAndPackages(directory);
            
            // Write a list of all the components and their source info.
            var sourcesYaml = new StringBuilder();
            foreach (var component in _components)
            {
                sourcesYaml.AppendLine($"- component: {component.Name}");
                sourcesYaml.AppendLine($"  source: {component.GitUrl}");
                sourcesYaml.AppendLine($"  branch: {component.Branch}");
                sourcesYaml.AppendLine($"  commit: {component.SourceRev.Commit}");
            }
            var sourcesPath = Path.Combine(directory, "sources.yml");
            if (File.Exists(sourcesPath))
            {
                File.Delete(sourcesPath);
            }
            File.WriteAllText(Path.Combine(directory, "sources.yml"), sourcesYaml.ToString());
        }

        public void PublishCache()
        {
            if (string.IsNullOrEmpty(_cacheDirectory))
            {
                throw new AptRepoToolException($"You must specify a {"cache".Quoted()} value.");
            }

            if (!Directory.Exists(_cacheDirectory))
            {
                Log.Information("Creating {directory}...", _cacheDirectory);
                Directory.CreateDirectory(_cacheDirectory);
            }

            // Publish the rootfs
            _rootfsExecutor.PublishCache(_cacheDirectory);

            // Publish each component.
            foreach (var component in Components)
            {
                component.PublishCache(_cacheDirectory);
            }
        }

        public string CacheDirectory => _cacheDirectory;

        public void AssertFixedCommits()
        {
            bool foundLatest = false;
            foreach (var component in Components)
            {
                if (component.SourceRev.Type == ComponentSrcRevType.Latest)
                {
                    Log.Error("The component {component} is referencing \"latest\" as a commit.", component.Name);
                    foundLatest = true;
                }
            }

            if (foundLatest)
            {
                throw new AptRepoToolException("One or more components were referencing \"latest\" as a commit.");
            }
        }
        
        public void AddComponent(IComponent component)
        {
            if (_components.Any(x => x.Name == component.Name))
            {
                throw new AptRepoToolException($"The component {component.Name.Quoted()} already exists.");
            }
            _components.Add(component);
        }

        public void SortComponentsTopologically()
        {
            var sorted = new List<IComponent>();
            var visited = new Dictionary<string, bool>();

            void Visit(IComponent component)
            {
                var alreadyVisited = visited.TryGetValue(component.Name, out var inProcess);

                if (alreadyVisited)
                {
                    if (inProcess)
                    {
                        throw new ArgumentException("Cyclic dependency found.");
                    }
                }
                else
                {
                    visited[component.Name] = true;

                    foreach (var dependency in component.Dependencies)
                    {
                        Visit(GetComponent(dependency));
                    }
                    
                    visited[component.Name] = false;
                    sorted.Add(component);
                }
            }
            
            foreach (var item in _components)
            {
                Visit(item);
            }

            _components = sorted;
        }
    }
}