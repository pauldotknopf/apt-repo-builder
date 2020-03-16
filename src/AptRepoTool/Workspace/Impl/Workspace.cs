using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AptRepoTool.BuildCache;
using AptRepoTool.Git;
using AptRepoTool.Rootfs;

namespace AptRepoTool.Workspace.Impl
{
    public class Workspace : IWorkspace
    {
        private readonly IRootfsExecutor _rootfsExecutor;
        private readonly IBuildCache _buildCache;
        private readonly IGitCache _gitCache;
        private List<IComponent> _components = new List<IComponent>();

        public Workspace(string rootDirectory,
            IRootfsExecutor rootfsExecutor,
            IBuildCache buildCache,
            IGitCache gitCache)
        {
            _rootfsExecutor = rootfsExecutor;
            _buildCache = buildCache;
            _gitCache = gitCache;
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

        public void BuildComponent(string name, bool force, bool bashPrompt)
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

            BuildRootfs(false);
            
            // Question, should we only force rebuild of the requested component? Or all dependencies?
            foreach (var component in sorted)
            {
                component.Build(force, bashPrompt);
            }
        }

        public void BuildRootfs(bool force)
        {
            _rootfsExecutor.BuildRoot(force);
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