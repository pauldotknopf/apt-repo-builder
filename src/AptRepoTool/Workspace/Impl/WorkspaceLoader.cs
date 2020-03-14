using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AptRepoTool.Git;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AptRepoTool.Workspace.Impl
{
    public class WorkspaceLoader : IWorkspaceLoader
    {
        private readonly IGitCache _gitCache;

        public WorkspaceLoader(IGitCache gitCache)
        {
            _gitCache = gitCache;
        }
        
        public IWorkspace Load(string workspaceDirectory)
        {
            var configFile = Path.Combine(workspaceDirectory, "config.yml");
            if (!File.Exists(configFile))
            {
                throw new AptRepoToolException("Couldn't find config.yml");
            }

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<RootConfigYaml>(File.ReadAllText(configFile));

            var workspace = new Workspace(workspaceDirectory);
            
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

                        var componentConfig =
                            deserializer.Deserialize<ComponentConfigYaml>(File.ReadAllText(componentConfigPath));
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
                            _gitCache));
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

        class RootConfigYaml
        {
            public List<string> Components { get; set; }
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