using System;
using System.IO;
using System.Security.Cryptography;
using AptRepoTool.Shell;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AptRepoTool.Rootfs.Impl
{
    public class DockerRootfsExecutor : IRootfsExecutor
    {
        private string _rootfsDirectory;
        private string _dockerfile;
        private IShellRunner _shellRunner;

        public DockerRootfsExecutor(IShellRunner shellRunner)
        {
            _shellRunner = shellRunner;
        }
        
        public void Configure(string rootfsDirectory)
        {
            _rootfsDirectory = rootfsDirectory;
            
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config =
                deserializer.Deserialize<DockerRootfsConfigYaml>(
                    File.ReadAllText(Path.Combine(rootfsDirectory, "config.yml")));
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (config == null)
                // ReSharper disable once HeuristicUnreachableCode
            {
                // ReSharper disable once HeuristicUnreachableCode
                config = new DockerRootfsConfigYaml();
            }

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
            
            using (var md5 = MD5.Create())
            {
                foreach (var file in Directory.GetFiles(_rootfsDirectory, "*", SearchOption.AllDirectories))
                {
                    using (var stream = File.OpenRead(file))
                    {
                        md5.ComputeHash(stream);
                    }
                }
                MD5Sum = BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();;
            }
        }

        public string MD5Sum { get; private set; }
        
        public void Build()
        {
            _shellRunner.RunShell($"docker build -f {_dockerfile.Quoted()} -t {GetImageName()} .", new RunnerOptions
            {
                WorkingDirectory = _rootfsDirectory
            });
        }

        private class DockerRootfsConfigYaml
        {
            public string Dockerfile { get; set; }
        }

        private string GetImageName()
        {
            return $"apt-repo-tool-rootfs:{MD5Sum.Substring(0, 20)}";
        }
    }
}