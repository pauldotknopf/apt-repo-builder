using System;
using System.IO;
using AptRepoTool.BuildCache;
using AptRepoTool.Git;
using AptRepoTool.Git.Impl;
using AptRepoTool.Shell;
using AptRepoTool.Shell.Impl;
using AptRepoTool.Workspace;
using AptRepoTool.Workspace.Impl;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AptRepoTool
{
    public class Helpers
    {
        public static string GetWorkspaceDirectory(string workspaceDirectory)
        {
            if (string.IsNullOrEmpty(workspaceDirectory))
            {
                workspaceDirectory = Directory.GetCurrentDirectory();
            }
            workspaceDirectory = Path.GetFullPath(workspaceDirectory);
            
            Log.Logger.Information("Workspace: {directory}", workspaceDirectory);

            return workspaceDirectory;
        }

        public static IServiceProvider BuildServiceProvider(string workspaceDirectory)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IWorkspaceLoader, WorkspaceLoader>();
            services.AddSingleton<IGitCache, GitCache>();
            services.AddSingleton<IShellRunner, ShellRunner>();
            services.Configure<GitCacheOptions>(options =>
            {
                options.GitCacheDir = Path.Combine(workspaceDirectory, ".git-cache");
            });
            services.Configure<BuildCacheOptions>(options =>
            {
                options.BuildCacheDir = Path.Combine(workspaceDirectory, ".build-cache");
            });
            services.AddSingleton<IBuildCache, BuildCache.Impl.BuildCache>();
            return services.BuildServiceProvider();
        }
    }
}