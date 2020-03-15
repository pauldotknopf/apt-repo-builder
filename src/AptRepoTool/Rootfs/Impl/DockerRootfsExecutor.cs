using System;
using System.IO;
using System.Security.Cryptography;
using AptRepoTool.Config;
using AptRepoTool.Shell;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
        
        public void Build()
        {
            _shellRunner.RunShell($"docker build -f {_dockerfile.Quoted()} -t {GetImageName()} .", new RunnerOptions
            {
                WorkingDirectory = _rootfsDirectory
            });
        }

        private string GetImageName()
        {
            return $"apt-repo-tool-rootfs:{MD5Sum.Substring(0, 20)}";
        }
    }
}