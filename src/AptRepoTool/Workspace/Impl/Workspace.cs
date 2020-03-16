using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AptRepoTool.Apt;
using AptRepoTool.BuildCache;
using AptRepoTool.Git;
using AptRepoTool.Rootfs;
using Serilog;

namespace AptRepoTool.Workspace.Impl
{
    public class Workspace : IWorkspace
    {
        private readonly IRootfsExecutor _rootfsExecutor;
        private readonly IAptHelper _aptHelper;
        private List<IComponent> _components = new List<IComponent>();

        public Workspace(string rootDirectory,
            IRootfsExecutor rootfsExecutor,
            IAptHelper aptHelper)
        {
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

            BuildRootfs(false);
            
            // Question, should we only force rebuild of the requested component? Or all dependencies?
            foreach (var component in sorted)
            {
                component.Build(
                    component.Name == name ? options.ForceBuild : options.ForceBuildDependencies,
                    options.PromptBeforeBuild);
            }
        }

        public void BuildRootfs(bool force)
        {
            _rootfsExecutor.BuildRoot(force);
        }

        public void PublishRepo(string directory)
        {
            Log.Information("Cleaning {directory}...", directory);
            directory.CleanOrCreateDirectory();

            foreach (var component in _components)
            {
                component.ExtractPackages(directory);
            }
            
            Log.Information("Indexing all source/binary packages...");
            _aptHelper.ScanSourcesAndPackages(directory);
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