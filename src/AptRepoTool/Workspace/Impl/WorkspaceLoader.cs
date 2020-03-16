using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AptRepoTool.BuildCache;
using AptRepoTool.Config;
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
        private readonly IConfigParser _configParser;

        public WorkspaceLoader(IGitCache gitCache,
            IShellRunner shellRunner,
            IBuildCache buildCache,
            IConfigParser configParser)
        {
            _gitCache = gitCache;
            _shellRunner = shellRunner;
            _buildCache = buildCache;
            _configParser = configParser;
        }
        
        public IWorkspace Load(string workspaceDirectory)
        {
            var configFile = Path.Combine(workspaceDirectory, "config.yml");
            if (!File.Exists(configFile))
            {
                throw new AptRepoToolException("Couldn't find config.yml");
            }

            var config = _configParser.LoadRootConfig(File.ReadAllText(configFile));
            var rootfsExecutor = GetRootfsExecutor(workspaceDirectory, config);
            var workspace = new Workspace(workspaceDirectory, rootfsExecutor, _buildCache, _gitCache);
            
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

                        var componentConfig = _configParser.LoadComponentConfig(File.ReadAllText(componentConfigPath));
                        if (componentConfig.Source == null)
                        {
                            throw new AptRepoToolException($"No {"source".Quoted()} was provided for {componentName.Quoted()}.");
                        }
                        if (string.IsNullOrEmpty(componentConfig.Source.Url))
                        {
                            throw new AptRepoToolException($"No {"url".Quoted()} provided for {componentName.Quoted()}.");
                        }
                        if (string.IsNullOrEmpty(componentConfig.Source.Branch))
                        {
                            throw new AptRepoToolException($"No {"branch".Quoted()} provided for {componentName.Quoted()}.");
                        }
                        if (string.IsNullOrEmpty(componentConfig.Source.Commit))
                        {
                            throw new AptRepoToolException($"No {"commit".Quoted()} provided for {componentName.Quoted()}.");
                        }
                        workspace.AddComponent(new Component(componentName,
                            componentConfig,
                            _gitCache,
                            workspace,
                            _buildCache,
                            _shellRunner,
                            rootfsExecutor));
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

        private IRootfsExecutor GetRootfsExecutor(string workspaceDirectory, RootConfig config)
        {
            if (string.IsNullOrEmpty(config.Rootfs))
            {
                config.Rootfs = "rootfs";
            }

            if (Path.IsPathRooted(config.Rootfs))
            {
                throw new AptRepoToolException($"Invalid rootfs value {config.Rootfs.Quoted()}");
            }

            var directory = Path.Combine(workspaceDirectory, config.Rootfs);
            if (!Directory.Exists(directory))
            {
                throw new AptRepoToolException($"The rootfs directory {config.Rootfs.Quoted()} doesn't exist.");
            }

            var rootfsConfigPath = Path.Combine(directory, "config.yml");
            if (!File.Exists(rootfsConfigPath))
            {
                throw new AptRepoToolException($"The config.yml doesn't exist in the rootfs directory.");
            }

            var rootfsConfig = _configParser.LoadRootfsConfig(File.ReadAllText(rootfsConfigPath));
            IRootfsExecutor executor;

            var md5Sum = "";
            using (var md5 = MD5.Create())
            {
                foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    using (var stream = File.OpenRead(file))
                    {
                        md5.ComputeHash(stream);
                    }
                }
                md5Sum = BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();;
            }
            
            if (rootfsConfig is DockerRootfsConfig dockerRootfsConfig)
            {
                executor = new DockerRootfsExecutor(_shellRunner, dockerRootfsConfig, md5Sum, directory);
            }
            else
            {
                throw new Exception("Unknown rootfs type.");
            }

            return executor;
        }

        // class RootfsConfigYaml
        // {
        //     public string Type { get; set; }
        // }
        //
        // class RootConfigYaml
        // {
        //     public List<string> Components { get; set; }
        //     
        //     public string RootFs { get; set; }
        // }
        //
        // class ComponentConfigYaml
        // {
        //     public string Url { get; set; }
        //     
        //     public string Branch { get; set; }
        //     
        //     public string Commit { get; set; }
        //     
        //     public List<string> Dependencies { get; set; }
        //     
        //     public string Type { get; set; }
        //     
        //     public string DebianDirectory { get; set; }
        // }
    }
}