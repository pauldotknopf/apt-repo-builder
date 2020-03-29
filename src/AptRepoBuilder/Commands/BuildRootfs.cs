using System.CommandLine;
using System.CommandLine.Invocation;
using AptRepoBuilder.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace AptRepoBuilder.Commands
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
                new Option(new []{"-f", "--force"})
                {
                    Name = "force",
                    Argument = new Argument<bool>()
                },
            };

            command.Handler = CommandHandler.Create(typeof(BuildRootfs).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(string workspaceDirectory, bool force)
        {
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);

            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            workspace.BuildRootfs(force);
        }
    }
}