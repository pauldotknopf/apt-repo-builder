using System;
using System.Collections.Generic;
using System.IO;
using AptRepoBuilder.Apt;
using AptRepoBuilder.BuildCache;
using AptRepoBuilder.Config;
using AptRepoBuilder.Git;
using AptRepoBuilder.Rootfs;
using AptRepoBuilder.Rootfs.Impl;
using AptRepoBuilder.Shell;
using Serilog;

namespace AptRepoBuilder.Workspace.Impl
{
    public class WorkspaceLoader : IWorkspaceLoader
    {
        private readonly IGitCache _gitCache;
        private readonly IShellRunner _shellRunner;
        private readonly IBuildCache _buildCache;
        private readonly IConfigParser _configParser;
        private readonly IAptHelper _aptHelper;

        public WorkspaceLoader(IGitCache gitCache,
            IShellRunner shellRunner,
            IBuildCache buildCache,
            IConfigParser configParser,
            IAptHelper aptHelper)
        {
            _gitCache = gitCache;
            _shellRunner = shellRunner;
            _buildCache = buildCache;
            _configParser = configParser;
            _aptHelper = aptHelper;
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
            var workspace = new Workspace(workspaceDirectory, rootfsExecutor, _aptHelper);
            
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
                            rootfsExecutor,
                            _aptHelper,
                            config));
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
            if (config.Rootfs == null)
            {
                config.Rootfs = new RootConfig.RootRootfsConfig();
            }
            
            if (string.IsNullOrEmpty(config.Rootfs.Dir))
            {
                config.Rootfs.Dir = "rootfs";
            }

            if (Path.IsPathRooted(config.Rootfs.Dir))
            {
                throw new AptRepoToolException($"Invalid rootfs value {config.Rootfs.Dir.Quoted()}");
            }

            var directory = Path.Combine(workspaceDirectory, config.Rootfs.Dir);
            if (!Directory.Exists(directory))
            {
                throw new AptRepoToolException($"The rootfs directory {config.Rootfs.Dir.Quoted()} doesn't exist.");
            }

            var rootfsConfigPath = Path.Combine(directory, "config.yml");
            if (!File.Exists(rootfsConfigPath))
            {
                throw new AptRepoToolException($"The config.yml doesn't exist in the rootfs directory.");
            }

            var rootfsConfig = _configParser.LoadRootfsConfig(File.ReadAllText(rootfsConfigPath));
            IRootfsExecutor executor;

            // Generate md5sum of entire directory, but ignore the ./tmp directory.
            var md5Sum = _shellRunner.ReadShell("find -type f -not -path \"./tmp/*\" -exec md5sum {} \\; | md5sum",
                new RunnerOptions
                {
                    WorkingDirectory = directory
                });
            md5Sum = md5Sum.Substring(0, md5Sum.IndexOf(" ", StringComparison.Ordinal)).Substring(0, 7);
            
            Log.Information("The rootfs checksum is {checksum}.", md5Sum);
            
            if (rootfsConfig is DockerRootfsConfig)
            {
                throw new AptRepoToolException("The docker rootfs type isn't supported.");
            }
            
            if (rootfsConfig is TarballRootfsConfig)
            {
                executor = new TarballRootfsExecutor(md5Sum, directory, _buildCache, _shellRunner);
            }
            else
            {
                throw new AptRepoToolException("Unknown rootfs type.");
            }

            return executor;
        }
    }
}