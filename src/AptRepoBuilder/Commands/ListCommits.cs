using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using AptRepoBuilder.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AptRepoBuilder.Commands
{
    public class ListCommits
    {
        public static Command Create()
        {
            var command = new Command("list-commits")
            {
                new Option(new []{"-d", "--workspace-directory"})
                {
                    Name = "workspace-directory",
                    Argument = new Argument<string>()
                },
            };

            command.Handler = CommandHandler.Create(typeof(ListCommits).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(string workspaceDirectory)
        {
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);

            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            foreach (var component in workspace.Components)
            {
                component.ResolveUnknownCommit();
            }

            foreach (var component in workspace.Components)
            {
                Log.Information("Component: {component}", component.Name);
                Log.Information("\t\tBranch: {branch}", component.Branch);
                Log.Information("\t\tCommit: {commit}", component.SourceRev.Commit);
            }
        }
    }
}