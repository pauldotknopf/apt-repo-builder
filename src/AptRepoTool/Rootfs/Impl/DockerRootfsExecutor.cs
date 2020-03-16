using System.Collections.Generic;
using System.IO;
using AptRepoTool.Config;
using AptRepoTool.Shell;
using Serilog;

namespace AptRepoTool.Rootfs.Impl
{
    public class DockerRootfsExecutor : IRootfsExecutor
    {
        private readonly string _rootfsDirectory;
        private readonly string _dockerfile;
        private readonly IShellRunner _shellRunner;

        public DockerRootfsExecutor(IShellRunner shellRunner,
            DockerRootfsConfig config,
            string md5Sum,
            string rootfsDirectory)
        {
            _shellRunner = shellRunner;
            _rootfsDirectory = rootfsDirectory;
            MD5Sum = md5Sum;

            if (string.IsNullOrEmpty(config.Dockerfile))
            {
                config.Dockerfile = "Dockerfile";
            }
            
            if (Path.IsPathRooted(config.Dockerfile))
            {
                throw new AptRepoToolException("Invalid Dockerfile path for rootfs.");
            }
            
            _dockerfile = Path.Combine(_rootfsDirectory, config.Dockerfile);
            
            if (!File.Exists(_dockerfile))
            {
                throw new AptRepoToolException($"The Dockerfile doesn't exist for the rootfs.");
            }
        }
        
        public string MD5Sum { get; }
        
        public void BuildRoot(bool force)
        {
            var imageId = _shellRunner.ReadShell($"docker images -q {GetImageName()}");
            if (!string.IsNullOrEmpty(imageId))
            {
                // The image is already built.
                if (force)
                {
                    Log.Warning("Forcing a rebuild of the rootfs...");
                }
                else
                {
                    Log.Information("The rootfs image is up to date, skipping build...");
                    return;
                }
            }
            else
            {
                Log.Information("Building rootfs...");
            }
            
            _shellRunner.RunShell($"docker build -f {_dockerfile.Quoted()} -t {GetImageName()} --no-cache .", new RunnerOptions
            {
                WorkingDirectory = _rootfsDirectory
            });
        }

        public void Run(string script, RunOptions options)
        {
            script.NotNullOrEmpty(nameof(script));
            script = script.Replace("\"", "\\\"");
            
            if (options == null)
            {
                options = new RunOptions();
            }

            var dockerArgs = new List<string>();

            if (options.Interactive)
            {
                dockerArgs.Add("-it");
            }
            
            foreach (var mount in options.Mounts)
            {
                dockerArgs.Add($"-v {mount.Source.Quoted()}:{mount.Target.Quoted()}");
            }

            foreach (var env in options.Env)
            {
                dockerArgs.Add($"-e {env.Key}={env.Value}");
            }

            // Pass the user id into the docker container.
            // This is so that the container has the
            // opportunity to run as that user.
            var userId = int.Parse(_shellRunner.ReadShell("id -u "));
            dockerArgs.Add($"-e USER_ID={userId}");
            
            _shellRunner.RunShell($"docker run --rm {string.Join(" ", dockerArgs)} {GetImageName()} /usr/bin/env bash -c \"{script}\"");
        }

        private string GetImageName()
        {
            return $"apt-repo-tool-rootfs:{MD5Sum.Substring(0, 20)}";
        }
    }
}