using System.CommandLine;
using System.CommandLine.Invocation;
using AptRepoTool.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace AptRepoTool.Commands
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
                }
            };

            command.Handler = CommandHandler.Create(typeof(BuildAll).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(bool force, string workspaceDirectory)
        {
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);

            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            workspace.BuildRootfs(false);
            
            foreach (var component in workspace.Components)
            {
                component.Build(force, false);
            }
        }
    }
}