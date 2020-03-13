using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AptRepoTool.Workspace.Impl
{
    public class WorkspaceLoader : IWorkspaceLoader
    {
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
                        var componentConfigPath = Path.Combine(childDirectory, "component.yml");
                        if (!File.Exists(componentConfigPath))
                        {
                            continue;
                        }

                        var componentConfig =
                            deserializer.Deserialize<ComponentConfigYaml>(File.ReadAllText(componentConfigPath));
                        if (componentConfig == null)
                        {
                            throw new AptRepoToolException($"Invalid component.yml file at {componentConfigPath.Quoted()}.");
                        }
                        workspace.AddComponent(new Component(Path.GetFileName(childDirectory), componentConfig.Dependencies));
                    }
                }
            }
            
            // Validate dependencies
            foreach (var component in workspace.Components)
            {
                foreach (var dependency in component.Dependencies)
                {
                    workspace.GetComponent(dependency);
                }
            }

            return workspace;
        }

        class RootConfigYaml
        {
            public List<string> Components { get; set; }
        }

        class ComponentConfigYaml
        {
            public string GitUrl { get; set; }
            public List<string> Dependencies { get; set; }
        }
    }
}