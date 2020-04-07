using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Reflection;
using System.Threading.Tasks;
using AptRepoBuilder.Commands;
using Serilog;

namespace AptRepoBuilder
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
                ListComponents.Create(),
                FetchComponents.Create(),
                BuildRootfs.Create(),
                BuildComponent.Create(),
                BuildAll.Create(),
                PublishRepo.Create(),
                PublishCache.Create()
            };
            rootCommand.Name = "apt-repo-tool";
            rootCommand.Description = "A tool to build an apt-repo from a deterministic set of inputs (git commits).";

            var builder = new CommandLineBuilder(rootCommand).UseDefaults();
            builder.UseExceptionHandler((exception, context) =>
            {
                void ProcessException(Exception ex)
                {
                    if (ex is AptRepoToolException aptRepoToolException)
                    {
                        Log.Logger.Error(aptRepoToolException.Message);
                        if (ex.InnerException != null)
                        {
                            Log.Logger.Error(ex.InnerException, ex.InnerException.Message);
                        }
                    }
                    else if (ex is OperationCanceledException)
                    {
                        Log.Logger.Error("The process was cancelled.");
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
