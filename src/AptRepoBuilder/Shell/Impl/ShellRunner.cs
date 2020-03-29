using System;
using System.IO;
using Serilog;

namespace AptRepoBuilder.Shell.Impl
{
    public class ShellRunner : IShellRunner
    {
        public void RunShell(string command, RunnerOptions runnerOptions = null)
        {
            if (runnerOptions == null)
            {
                runnerOptions = new RunnerOptions();
            }

            var workingDirectory = runnerOptions.WorkingDirectory;
            if (string.IsNullOrEmpty(workingDirectory))
            {
                workingDirectory = Directory.GetCurrentDirectory();
            }
            
            var escapedArgs = command.Replace("\"", "\\\"");
            
            var sudoWhitelist = "";
            if (runnerOptions.Env != null && runnerOptions.Env.Count > 0)
            {
                sudoWhitelist = $"--preserve-env={string.Join(",", runnerOptions.Env.Keys)}";
            }
            
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/bin/env",
                Arguments = $"{(runnerOptions.UseSudo ? $"sudo {sudoWhitelist} " : "")}bash -c \"{escapedArgs}\"",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                WorkingDirectory = workingDirectory
            };

            if (runnerOptions.Env != null)
            {
                foreach (var envValue in runnerOptions.Env)
                {
                    processStartInfo.Environment.Add(envValue.Key, envValue.Value);
                }
            }

            var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                throw new Exception("Couldn't create process.");
            }
            
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Log.Error("Error executing command: {command}", command);
                throw new Exception($"Exit code: {process.ExitCode}");
            }
        }

        public string ReadShell(string command, RunnerOptions runnerOptions = null)
        {
            if (runnerOptions == null)
            {
                runnerOptions = new RunnerOptions();
            }
            
            var workingDirectory = runnerOptions.WorkingDirectory;
            if (string.IsNullOrEmpty(workingDirectory))
            {
                workingDirectory = Directory.GetCurrentDirectory();
            }
            
            var escapedArgs = command.Replace("\"", "\\\"");

            var sudoWhitelist = "";
            if (runnerOptions.Env != null && runnerOptions.Env.Count > 0)
            {
                sudoWhitelist = $"--preserve-env={string.Join(",", runnerOptions.Env.Keys)}";
            }
            
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/bin/env",
                Arguments = $"{(runnerOptions.UseSudo ? $"sudo {sudoWhitelist} " : "")}bash -c \"{escapedArgs}\"",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardOutput = true,
                WorkingDirectory = workingDirectory
            };

            if (runnerOptions.Env != null)
            {
                foreach (var envValue in runnerOptions.Env)
                {
                    processStartInfo.Environment.Add(envValue.Key, envValue.Value);
                }
            }

            
            var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                throw new Exception("Couldn't create process.");
            }

            var output = process.StandardOutput.ReadToEnd();

            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                Log.Error("Error executing command: {command}", command);
                Log.Error("Command output: {output}", output);
                throw new Exception($"Exit code: {process.ExitCode}");
            }

            return output;
        }
    }
}