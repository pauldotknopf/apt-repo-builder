using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AptRepoTool.BuildCache;
using AptRepoTool.Git;
using AptRepoTool.Rootfs;
using AptRepoTool.Rootfs.Impl;
using AptRepoTool.Shell;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AptRepoTool.Workspace.Impl
{
    public class WorkspaceLoader : IWorkspaceLoader
    {
        private readonly IGitCache _gitCache;
        private readonly IShellRunner _shellRunner;
        private readonly IBuildCache _buildCache;

        private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        public WorkspaceLoader(IGitCache gitCache,
            IShellRunner shellRunner,
            IBuildCache buildCache)
        {
            _gitCache = gitCache;
            _shellRunner = shellRunner;
            _buildCache = buildCache;
        }
        
        public IWorkspace Load(string workspaceDirectory)
        {
            var configFile = Path.Combine(workspaceDirectory, "config.yml");
            if (!File.Exists(configFile))
            {
                throw new AptRepoToolException("Couldn't find config.yml");
            }

            var config = _yamlDeserializer.Deserialize<RootConfigYaml>(File.ReadAllText(configFile));
            var workspace = new Workspace(workspaceDirectory, GetRootfsExecutor(workspaceDirectory, config), _buildCache, _gitCache);
            
            if (config.Components != null)
            {
                foreach (var component in config.Components)
                {
                    if (Path.IsPathRooted(component))
                    {
                        throw new AptRepoToolException($"Invalid component path {component.Quoted()}.");
                    }

                    var componentPath = Path.Combine(workspaceDirectory, component);

                    if (!Directory.Exists(componentPath))
                    {
                        throw new AptRepoToolException($"Component path {component.Quoted()} doesn't exist.");
                    }

                    foreach (var childDirectory in Directory.GetDirectories(componentPath))
                    {
                        var componentName = Path.GetFileName(childDirectory);
                        var componentConfigPath = Path.Combine(childDirectory, "component.yml");
                        if (!File.Exists(componentConfigPath))
                        {
                            continue;
                        }

                        var componentConfig = _yamlDeserializer.Deserialize<ComponentConfigYaml>(File.ReadAllText(componentConfigPath));
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        // ReSharper disable once HeuristicUnreachableCode   
                        if (componentConfig == null)
                        {
                            // ReSharper disable once HeuristicUnreachableCode
                            throw new AptRepoToolException($"Invalid component.yml file at {componentConfigPath.Quoted()}.");
                        }

                        if (string.IsNullOrEmpty(componentConfig.GitUrl))
                        {
                            throw new AptRepoToolException($"No {"gitUrl".Quoted()} provided for {componentName.Quoted()}.");
                        }
                        if (string.IsNullOrEmpty(componentConfig.Branch))
                        {
                            throw new AptRepoToolException($"No {"branch".Quoted()} provided for {componentName.Quoted()}.");
                        }
                        if (string.IsNullOrEmpty(componentConfig.Revision))
                        {
                            throw new AptRepoToolException($"No {"revision".Quoted()} provided for {componentName.Quoted()}.");
                        }
                        workspace.AddComponent(new Component(componentName,
                            componentConfig.Dependencies,
                            componentConfig.GitUrl,
                            componentConfig.Branch,
                            componentConfig.Revision,
                            _gitCache,
                            workspace,
                            _buildCache,
                            _shellRunner));
                    }
                }
            }
            
            // Validate dependencies
            foreach (var component in workspace.Components)
            {
                foreach (var dependency in component.Dependencies)
                {
                    // Will throw if component not found.
                    workspace.GetComponent(dependency);
                }
            }
            
            // Validate recursion
            void ProcessComponent(IComponent component, HashSet<string> stack)
            {
                stack.Add(component.Name);
                foreach (var dependency in component.Dependencies)
                {
                    if (stack.Contains(dependency))
                    {
                        throw new AptRepoToolException($"Cycle detected {component.Name.Quoted()} <> {dependency.Quoted()}.");
                    }
                    var stackCopy = new HashSet<string>(stack);
                    stackCopy.Add(dependency);
                    ProcessComponent(workspace.GetComponent(dependency), stackCopy);
                }
            }
            foreach (var component in workspace.Components)
            {
                ProcessComponent(component, new HashSet<string>());
            }
            
            workspace.SortComponentsTopologically();
            
            return workspace;
        }

        private IRootfsExecutor GetRootfsExecutor(string workspaceDirectory, RootConfigYaml config)
        {
            if (string.IsNullOrEmpty(config.RootFs))
            {
                config.RootFs = "rootfs";
            }

            if (Path.IsPathRooted(config.RootFs))
            {
                throw new AptRepoToolException($"Invalid rootfs value {config.RootFs.Quoted()}");
            }

            var directory = Path.Combine(workspaceDirectory, config.RootFs);
            if (!Directory.Exists(directory))
            {
                throw new AptRepoToolException($"The rootfs directory {config.RootFs.Quoted()} doesn't exist.");
            }

            var rootfsConfigPath = Path.Combine(directory, "config.yml");
            if (!File.Exists(rootfsConfigPath))
            {
                throw new AptRepoToolException($"The config.yml doesn't exist in the rootfs directory.");
            }

            var rootfsConfig = _yamlDeserializer.Deserialize<RootfsConfigYaml>(File.ReadAllText(rootfsConfigPath));
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (rootfsConfig == null)
            // ReSharper disable once HeuristicUnreachableCode
            {
                // ReSharper disable once HeuristicUnreachableCode
                rootfsConfig = new RootfsConfigYaml();
            }

            IRootfsExecutor executor;
            
            switch (rootfsConfig.Type)
            {
                case "docker":
                    executor = new DockerRootfsExecutor(_shellRunner);
                    break;
                default:
                    throw new AptRepoToolException($"Invalid rootfs type");
            }
            
            executor.Configure(directory);

            return executor;
        }

        class RootfsConfigYaml
        {
            public string Type { get; set; }
        }
        
        class RootConfigYaml
        {
            public List<string> Components { get; set; }
            
            public string RootFs { get; set; }
        }

        class ComponentConfigYaml
        {
            public string GitUrl { get; set; }
            
            public string Branch { get; set; }
            
            public string Revision { get; set; }
            
            public List<string> Dependencies { get; set; }
        }
    }
}