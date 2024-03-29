using System.CommandLine;
using System.CommandLine.Invocation;
using AptRepoBuilder.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace AptRepoBuilder.Commands
{
    public class BuildComponent
    {
        public static Command Create()
        {
            var command = new Command("build-component")
            {
                new Argument<string>("name"),
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
                new Option(new []{"-fd", "--force-dependencies"})
                {
                    Name = "force-dependencies",
                    Argument = new Argument<bool>()
                },
                new Option(new []{"-pbb", "--prompt-before-build"})
                {
                    Name = "prompt-before-build",
                    Argument = new Argument<bool>()
                },
            };

            command.Handler = CommandHandler.Create(typeof(BuildComponent).GetMethod(nameof(Run)));
            
            return command;
        }
        
        public static void Run(string name, bool force, bool forceDependencies, bool promptBeforeBuild, string workspaceDirectory)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new AptRepoToolException($"You must provide a component name to build.");
            }
            
            workspaceDirectory = Helpers.GetWorkspaceDirectory(workspaceDirectory);

            var workspace = Helpers.BuildServiceProvider(workspaceDirectory).GetRequiredService<IWorkspaceLoader>()
                .Load(workspaceDirectory);

            workspace.BuildComponent(name, new ComponentBuildOptions
            {
                ForceBuild = force,
                ForceBuildDependencies = forceDependencies,
                PromptBeforeBuild = promptBeforeBuild
            });
        }
    }
}