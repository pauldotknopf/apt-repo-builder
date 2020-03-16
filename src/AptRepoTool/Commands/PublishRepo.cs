using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using AptRepoTool.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AptRepoTool.Commands
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
                }
            };

            command.Handler = CommandHandler.Create(typeof(PublishRepo).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(string workspaceDirectory, string output)
        {
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);
            if (string.IsNullOrEmpty(output))
            {
                output = Path.Combine(workspaceDirectory, "repo");
            }
            
            Log.Information("Publishing apt repo to {output}...", output);
            
            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            workspace.BuildRootfs(false);

            foreach (var component in workspace.Components)
            {
                component.Build(false, false);
            }
            
            workspace.PublishRepo(output);
        }
    }
}