using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using AptRepoTool.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AptRepoTool.Commands
{
    public class ListComponents
    {
        public static Command Create()
        {
            var command = new Command("list-components")
            {
                new Option(new []{"-d", "--workspace-directory"})
                {
                    Name = "workspace-directory",
                    Argument = new Argument<string>()
                },
            };

            command.Handler = CommandHandler.Create(typeof(ListComponents).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(string workspaceDirectory)
        {
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);

            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            foreach (var component in workspace.Components)
            {
                Log.Logger.Information("{component}", component.Name);
                component.FetchSources();
            }
        }
    }
}