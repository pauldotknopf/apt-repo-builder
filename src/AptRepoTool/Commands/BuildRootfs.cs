using System.CommandLine;
using System.CommandLine.Invocation;
using AptRepoTool.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace AptRepoTool.Commands
{
    public class BuildRootfs
    {
        public static Command Create()
        {
            var command = new Command("build-rootfs")
            {
                new Option(new []{"-d", "--workspace-directory"})
                {
                    Name = "workspace-directory",
                    Argument = new Argument<string>()
                },
            };

            command.Handler = CommandHandler.Create(typeof(BuildRootfs).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(string workspaceDirectory)
        {
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);

            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            workspace.BuildRootfs();
        }
    }
}