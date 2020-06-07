using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using AptRepoBuilder.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace AptRepoBuilder.Commands
{
    public class BuildAll
    {
        public static Command Create()
        {
            var command = new Command("build-all")
            {
                new Option(new []{"-d", "--workspace-directory"})
                {
                    Name = "workspace-directory",
                    Argument = new Argument<string>()
                },
                new Option(new []{"-f", "--force"})
                {
                    Name = "force",
                    Argument = new Argument<bool>()
                },
                new Option(new []{"--disable-latest"})
                {
                    Name = "disable-latest",
                    Argument = new Argument<bool>()
                }
            };

            command.Handler = CommandHandler.Create(typeof(BuildAll).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(bool force, string workspaceDirectory, bool disableLatest)
        {
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);

            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            if (disableLatest || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APT_REPO_BUILDER_DISABLE_LATEST")))
            {
                workspace.AssertFixedCommits();
            }
            
            workspace.BuildRootfs(false);
            
            foreach (var component in workspace.Components)
            {
                component.Build(force, false);
            }
        }
    }
}