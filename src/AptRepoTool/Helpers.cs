using System;
using System.IO;
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

        public static IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IWorkspaceLoader, WorkspaceLoader>();
            return services.BuildServiceProvider();
        }
    }
}