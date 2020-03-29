using System.CommandLine;
using System.CommandLine.Invocation;
using AptRepoBuilder.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace AptRepoBuilder.Commands
{
    public class FetchComponents
    {
        public static Command Create()
        {
            var command = new Command("fetch-components")
            {
                new Option(new []{"-d", "--workspace-directory"})
                {
                    Name = "workspace-directory",
                    Argument = new Argument<string>()
                },
            };

            command.Handler = CommandHandler.Create(typeof(FetchComponents).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(string workspaceDirectory)
        {
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);

            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            foreach (var component in workspace.Components)
            {
                component.FetchSources();
            }
        }
    }
}