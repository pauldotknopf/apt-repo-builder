using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Reflection;
using System.Threading.Tasks;
using Serilog;

namespace AptRepoTool
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
            
           
            
            var rootCommand = new RootCommand
            {
                Commands.ListComponents.Create()
            };
            rootCommand.Name = "apt-repo-tool";
            rootCommand.Description = "A tool to build an apt-repo from a deterministic set of inputs (git commits).";

            var builder = new CommandLineBuilder(rootCommand);
            builder.UseExceptionHandler((exception, context) =>
            {
                void ProcessException(Exception ex)
                {
                    if (ex is AptRepoToolException aptRepoToolException)
                    {
                        Log.Logger.Error(aptRepoToolException.Message);
                    }
                    else if (ex is TargetInvocationException targetInvocationException)
                    {
                        ProcessException(targetInvocationException.InnerException);
                    }
                    else
                    {
                        Log.Logger.Error(ex, "An unhandled exception occured.");
                    }
                }
                
                ProcessException(exception);
                
                context.ResultCode = 1;
            });

            return await builder.Build().Parse(args).InvokeAsync();
        }
    }
}
