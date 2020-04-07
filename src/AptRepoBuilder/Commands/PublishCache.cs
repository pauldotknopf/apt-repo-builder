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
            };

            command.Handler = CommandHandler.Create(typeof(PublishCache).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(string workspaceDirectory)
        {
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);

            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            workspace.PublishCache();
        }
    }
}