using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using AptRepoBuilder.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AptRepoBuilder.Commands
{
    public class PublishRepo
    {
        public static Command Create()
        {
            var command = new Command("publish-repo")
            {
                new Option(new []{"-d", "--workspace-directory"})
                {
                    Name = "workspace-directory",
                    Argument = new Argument<string>()
                },
                new Option(new []{"-o", "--output"})
                {
                    Name = "output",
                    Argument = new Argument<string>()
                },
                new Option(new []{"--disable-latest"})
                {
                    Name = "disable-latest",
                    Argument = new Argument<bool>()
                },
                new Option(new []{"--publish-cache"})
                {
                    Name = "publish-cache",
                    Argument = new Argument<bool>()
                },
            };

            command.Handler = CommandHandler.Create(typeof(PublishRepo).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(string workspaceDirectory, string output, bool disableLatest, bool publishCache)
        {
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);
            if (string.IsNullOrEmpty(output))
            {
                output = Path.Combine(workspaceDirectory, "repo");
            }
            
            Log.Information("Publishing apt repo to {output}...", output);
            
            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            if (disableLatest || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APT_REPO_BUILDER_DISABLE_LATEST")))
            {
                workspace.AssertFixedCommits();
            }
            
            workspace.BuildRootfs(false);

            foreach (var component in workspace.Components)
            {
                component.Build(false, false);
            }
            
            workspace.PublishRepo(output);

            if (publishCache)
            {
                workspace.PublishCache();
            }
        }
    }
}