using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using AptRepoBuilder.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AptRepoBuilder.Commands
{
    public class PublishCache
    {
        public static Command Create()
        {
            var command = new Command("publish-cache")
            {
                new Option(new []{"-d", "--workspace-directory"})
                {
                    Name = "workspace-directory",
                    Argument = new Argument<string>()
                },
                new Option(new []{"--disable-latest"})
                {
                    Name = "disable-latest",
                    Argument = new Argument<bool>()
                }
            };

            command.Handler = CommandHandler.Create(typeof(PublishCache).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(string workspaceDirectory, bool disableLatest)
        {
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);

            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            if (disableLatest || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APT_REPO_BUILDER_DISABLE_LATEST")))
            {
                workspace.AssertFixedCommits();
            }
            
            workspace.PublishCache();
        }
    }
}