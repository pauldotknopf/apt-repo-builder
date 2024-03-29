using System;
using System.Collections.Generic;
using System.IO;
using AptRepoBuilder.Shell;
using Microsoft.Extensions.Options;

namespace AptRepoBuilder.Git.Impl
{
    public class GitCache : IGitCache
    {
        private readonly IShellRunner _shellRunner;
        private readonly GitCacheOptions _options;
        private const string BaseGitCommand = "git -c core.fsyncobjectfiles=0";
        private HashSet<string> _repoFetched = new HashSet<string>();
        private Dictionary<string, string> _lastCommit = new Dictionary<string, string>();
        
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
            var key = $"{url}:{branch}";
            if (_lastCommit.ContainsKey(key))
            {
                return _lastCommit[key];
            }
            
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
                        _lastCommit.Add(key, split[0]);
                        return _lastCommit[key];
                    }
                    line = reader.ReadLine();
                }
            }
            
            throw new AptRepoToolException($"No upstream branch {branch.Quoted()} found at {url.Quoted()}.");
        }

        public void Extract(string url, string branch, string commit, string destination)
        {
            var mirrorDirectory = GetMirrorDirectory(url);

            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }
            else
            {
                foreach (var directory in Directory.GetDirectories(destination, "*", SearchOption.TopDirectoryOnly))
                {
                    Directory.Delete(directory, true);
                }

                foreach (var file in Directory.GetFiles(destination))
                {
                    File.Delete(file);
                }
            }
            
            Run($"{BaseGitCommand} clone -s -n {mirrorDirectory.Quoted()} {destination.Quoted()}");
            Run($"{BaseGitCommand} remote set-url origin {url.Quoted()}", destination);
            Run($"{BaseGitCommand} checkout -B {branch} {commit}", destination);
            Run($"{BaseGitCommand} branch {branch} --set-upstream-to origin/{branch}", destination);
            Run($"{BaseGitCommand} checkout {commit}", destination);
            Run($"{BaseGitCommand} submodule update --init --recursive", destination);
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
                        {"LANG", "C"},
                        // {"GIT_TRACE", "1"},
                        // {"GIT_TRANSFER_TRACE", "1"},
                        // {"GIT_CURL_VERBOSE", "1"}
                    }
                });
        }
    }
}