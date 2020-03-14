using System;
using System.Collections.Generic;
using System.IO;
using AptRepoTool.Shell;
using Microsoft.Extensions.Options;

namespace AptRepoTool.Git.Impl
{
    public class GitCache : IGitCache
    {
        private readonly IShellRunner _shellRunner;
        private readonly GitCacheOptions _options;
        private const string BaseGitCommand = "git -c core.fsyncobjectfiles=0";
        private HashSet<string> _repoFetched = new HashSet<string>();
        
        public GitCache(IOptions<GitCacheOptions> options, IShellRunner shellRunner)
        {
            _shellRunner = shellRunner;
            _options = options.Value;
        }

        public void Fetch(string url)
        {
            if (_repoFetched.Contains(url))
            {
                // We only fetch a repo once during run of the process.
                return;
            }
            
            var mirrorDirectory = GetMirrorDirectory(url);
            
            if (!Directory.Exists(mirrorDirectory))
            {
                Run($"{BaseGitCommand} clone --bare --mirror {url.Quoted()} {mirrorDirectory.Quoted()} --progress");
            }
            else
            {
                // Re-fetch
                var output = Read($"{BaseGitCommand} remote", mirrorDirectory);
                if (output.Contains("origin"))
                {
                    Run($"{BaseGitCommand} remote rm origin", mirrorDirectory);
                }
                Run($"{BaseGitCommand} remote add --mirror=fetch origin {url.Quoted()}", mirrorDirectory);
                Run($"{BaseGitCommand} fetch -f --prune --progress {url.Quoted()} refs/*:refs/*", mirrorDirectory);
                Run($"{BaseGitCommand} prune-packed", mirrorDirectory);
                Run($"{BaseGitCommand} pack-refs --all", mirrorDirectory);
                Run($"{BaseGitCommand} pack-redundant --all | xargs -r rm", mirrorDirectory);
            }

            _repoFetched.Add(url);
        }

        public bool ContainsBranchAndCommit(string url, string branch, string commit)
        {
            var mirrorDirectory = GetMirrorDirectory(url);
            var output = _shellRunner.ReadShell($"{BaseGitCommand} branch --contains {commit} --list {branch} 2> /dev/null | wc -l",
                new RunnerOptions
                {
                    WorkingDirectory = mirrorDirectory,
                    Env = new Dictionary<string, string>
                    {
                        { "LANG", "C" }
                    }
                });
            var split = output.Split(Environment.NewLine);
            if (split.Length != 2)
            {
                throw new Exception("Unexpected output");
            }
            return split[0] != "0";
        }

        public string GetLatestCommit(string url, string branch)
        {
            var output = _shellRunner.ReadShell($"{BaseGitCommand} ls-remote {url.Quoted()}",
                new RunnerOptions
                {
                    Env = new Dictionary<string, string>
                    {
                        { "LANG", "C" }
                    }
                });

            using (var reader = new StringReader(output))
            {
                var line = reader.ReadLine();
                while (line != null)
                {
                    var split = line.Split("\t");
                    if (split[1] == $"refs/heads/{branch}")
                    {
                        return split[0];
                    }
                    line = reader.ReadLine();
                }
            }
            
            throw new AptRepoToolException($"No upstream branch {branch.Quoted()} found at {url.Quoted()}.");
        }

        private string GetMirrorDirectory(string url)
        {
            var cacheDirectory = _options.GitCacheDir;
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            return Path.Combine(cacheDirectory, url.CalculateMD5Hash());
        }

        private string Read(string command, string directory = null)
        {
            return _shellRunner.ReadShell(
                command,
                new RunnerOptions
                {
                    WorkingDirectory = directory,
                    Env = new Dictionary<string, string>
                    {
                        {"LANG", "C"}
                    }
                });
        }
        
        private void Run(string command, string directory = null)
        {
            _shellRunner.RunShell(
                command,
                new RunnerOptions
                {
                    WorkingDirectory = directory,
                    Env = new Dictionary<string, string>
                    {
                        {"LANG", "C"}
                    }
                });
        }
    }
}