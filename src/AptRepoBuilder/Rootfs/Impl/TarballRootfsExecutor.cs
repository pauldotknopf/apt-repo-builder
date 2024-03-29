using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AptRepoBuilder.BuildCache;
using AptRepoBuilder.Shell;
using Serilog;

namespace AptRepoBuilder.Rootfs.Impl
{
    public class TarballRootfsExecutor : IRootfsExecutor
    {
        private readonly string _rootfsDirectory;
        private readonly IBuildCache _buildCache;
        private readonly IShellRunner _shellRunner;

        public TarballRootfsExecutor(string md5Sum,
            string rootfsDirectory,
            IBuildCache buildCache,
            IShellRunner shellRunner)
        {
            _rootfsDirectory = rootfsDirectory;
            _buildCache = buildCache;
            _shellRunner = shellRunner;
            MD5Sum = md5Sum;
        }
        
        public string MD5Sum { get; }
        
        public void BuildRoot(bool force)
        {
            var buildKey = $"rootfs-{MD5Sum}";
           
            if (_buildCache.HasCacheDirectory(buildKey))
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
                // Let's see if the rootfs exists in the cache.
                Log.Information("Building rootfs...");
            }

            using (var buildCache = _buildCache.StartSession(buildKey, true))
            {
                var buildFile = Path.Combine(_rootfsDirectory, "build.sh");
                if (!File.Exists(buildFile))
                {
                    throw new AptRepoToolException($"The file build.sh doesn't exist in the rootfs directory.");
                }

                var targetRootfsFile = Path.Combine(buildCache.Dir, "rootfs.tar.gz");

                _shellRunner.RunShell("./build.sh", new RunnerOptions
                {
                    UseSudo = true,
                    WorkingDirectory = _rootfsDirectory,
                    Env = new Dictionary<string, string>
                    {
                        {"ARB_DEST_ROOTFS_DIR", buildCache.Dir},
                        {"ARB_DEST_ROOTFS_FILE_NAME", "rootfs.tar.gz"},
                        {"ARB_DEST_ROOTFS_FILE_PATH", targetRootfsFile }
                    }
                });
                _shellRunner.RunShell($"chmod 777 {targetRootfsFile}", new RunnerOptions
                {
                    UseSudo = true,
                    WorkingDirectory = _rootfsDirectory
                });

                if (!File.Exists(targetRootfsFile))
                {
                    throw new AptRepoToolException(
                        $"The file rootfs.tar.gz wasn't created at {"$ARB_DEST_ROOTFS_FILE_PATH".Quoted()}.");
                }
                
                buildCache.Commit();
            }
        }

        public void CheckCache(string directory)
        {
            var buildKey = $"rootfs-{MD5Sum}";
           
            if (_buildCache.HasCacheDirectory(buildKey))
            {
                // No need to check cache, we have it locally already.
                return;
            }

            var cacheFile = Path.Combine(directory, $"{buildKey}.tar.gz");
            if (File.Exists(cacheFile))
            {
                Log.Information("Restoring rootfs from cache...");
                using (var buildCache = _buildCache.StartSession(buildKey, true))
                {
                    File.Copy(cacheFile, Path.Combine(buildCache.Dir, "rootfs.tar.gz"));
                    buildCache.Commit();
                }
                Log.Information("Successfully restored rootfs from cache.");
            }
        }

        public void PublishCache(string directory)
        {
            Log.Information("Publishing the rootfs into the cache...");
            var buildKey = $"rootfs-{MD5Sum}";

            var cacheDestination = Path.Combine(directory, $"{buildKey}.tar.gz");
            if (File.Exists(cacheDestination))
            {
                Log.Information("The rootfs already exists in the cache, ignoring...");
                return;
            }
            
            if (!_buildCache.HasCacheDirectory(buildKey))
            {
                throw new AptRepoToolException("The rootfs has not been built.");
            }

            var cacheDestinationTmp = $"{cacheDestination}.tmp";
            if (File.Exists(cacheDestinationTmp))
            {
                File.Delete(cacheDestinationTmp);
            }
            File.Copy(Path.Combine(_buildCache.GetCacheDirectory(buildKey), "rootfs.tar.gz"), cacheDestinationTmp);
            File.Move(cacheDestinationTmp, cacheDestination);
        }

        public IRootfsSession StartSession(RunOptions options)
        {
            Log.Information("Building the rootfs environment...");
            
            if (!_buildCache.HasCacheDirectory(GetRootfsCacheKey()))
            {
                throw new AptRepoToolException($"The rootsf isn't build.");
            }
            
            var rootfsTarball = Path.Combine(_buildCache.GetCacheDirectory($"rootfs-{MD5Sum}"),
                "rootfs.tar.gz");

            var runSession = _buildCache.StartSession("rootfs-run", false);
            var mountedMounts = new List<string>();
            
            var runnerOptions = new RunnerOptions
            {
                UseSudo = true,
                WorkingDirectory = runSession.Dir
            };
            
            try
            {
                // Before we run, let's make sure there aren't dangling mounts from a previous build.
                // /home/pknopf/git/apt-repo-tool-example/.build-cache/rootfs-run/rootfs/workspace/build
                var mounts = _shellRunner.ReadShell("cat /proc/mounts");
                var dangling = new List<string>();
                using (var stringReader = new StringReader(mounts))
                {
                    var line = stringReader.ReadLine();
                    while (line != null)
                    {
                        var split = line.Split(" ");
                        if (split[1].StartsWith(runSession.Dir))
                        {
                            dangling.Add(split[1]);
                        }
                        line = stringReader.ReadLine();
                    }
                }
                dangling.Reverse(); // Important to reverse, so that /dev/pts is unmounted before /dev
                foreach (var danglingMount in dangling)
                {
                    Log.Warning("Dangling mount {mount} was found, unmounting before chroot.", danglingMount);
                    _shellRunner.RunShell($"umount {danglingMount}", runnerOptions);
                }
                
                _shellRunner.RunShell("find . -mindepth 1 -delete", runnerOptions);
                _shellRunner.RunShell("mkdir rootfs", runnerOptions);
                _shellRunner.RunShell($"tar -C rootfs -xf {rootfsTarball.Quoted()}", runnerOptions);
                
                if (options.Mounts != null)
                {
                    foreach (var mount in options.Mounts)
                    {
                        var destination = Path.Combine(runnerOptions.WorkingDirectory, "rootfs") + mount.Target;
                        var isDir = (File.GetAttributes(mount.Source) & FileAttributes.Directory) == FileAttributes.Directory;
                        if (isDir)
                        {
                            _shellRunner.RunShell($"mkdir -p {destination} && mount -obind {mount.Source} {destination}", runnerOptions);
                            mountedMounts.Add(destination);
                        }
                        else
                        {
                            _shellRunner.RunShell($"mkdir -p {Path.GetDirectoryName(destination)} " +
                                                  $"&& rm -rf {destination} " +
                                                  $"&& touch {destination} " +
                                                  $"&& mount -obind {mount.Source} {destination}", runnerOptions);
                            mountedMounts.Add(destination);
                        }
                    }
                }

                var procDir = Path.Combine(runnerOptions.WorkingDirectory, "rootfs", "proc");
                _shellRunner.RunShell($"mount proc {procDir.Quoted()} -t proc -o nosuid,noexec,nodev", runnerOptions);
                mountedMounts.Add(procDir);

                var sysDir = Path.Combine(runnerOptions.WorkingDirectory, "rootfs", "sys");
                _shellRunner.RunShell($"mount sys {sysDir.Quoted()} -t sysfs -o nosuid,noexec,nodev,ro", runnerOptions);
                mountedMounts.Add(sysDir);

                var devDir = Path.Combine(runnerOptions.WorkingDirectory, "rootfs", "dev");
                _shellRunner.RunShell($"mount udev {devDir.Quoted()} -t devtmpfs -o mode=0755,nosuid", runnerOptions);
                mountedMounts.Add(devDir);

                var devPtrDir = Path.Combine(devDir, "pts");
                _shellRunner.RunShell($"mount devpts {devPtrDir.Quoted()} -t devpts -o mode=0620,gid=5,nosuid,noexec", runnerOptions);
                mountedMounts.Add(devPtrDir);
                    
                var shmDir = Path.Combine(devDir, "shm");
                _shellRunner.RunShell($"mount shm {shmDir.Quoted()} -t tmpfs -o mode=1777,nosuid,nodev", runnerOptions);
                mountedMounts.Add(shmDir);

                return new RootfsSession(runSession, mountedMounts, _shellRunner);
            }
            catch (Exception)
            {
                // Unmount anything that may have been already mounted.
                var mountsReverse = mountedMounts.ToList();
                mountsReverse.Reverse();
                foreach (var mount in mountsReverse)
                {
                    _shellRunner.RunShell($"umount {mount}", runnerOptions);
                }
                
                runSession.Dispose();
                
                throw;
            }
        }

        private string GetRootfsCacheKey()
        {
            return $"rootfs-{MD5Sum.Substring(0, 7)}";
        }

        class RootfsSession : IRootfsSession
        {
            private readonly ICacheSession _rootfsCacheSession;
            private readonly List<string> _mounts;
            private readonly IShellRunner _shellRunner;
            private bool _disposed = false;

            public RootfsSession(ICacheSession rootfsCacheSession, List<string> mounts, IShellRunner shellRunner)
            {
                _rootfsCacheSession = rootfsCacheSession;
                _mounts = mounts;
                _shellRunner = shellRunner;
            }
            
            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                
                // Unmount anything that may have been already mounted.
                var mountsReverse = _mounts.ToList();
                mountsReverse.Reverse();
                foreach (var mount in mountsReverse)
                {
                    _shellRunner.RunShell($"umount {mount}", new RunnerOptions
                    {
                        UseSudo = true,
                        WorkingDirectory = _rootfsCacheSession.Dir
                    });
                }
                
                _rootfsCacheSession.Dispose();
            }

            public void Run(string command, Dictionary<string, string> env = null)
            {
                var envParam = "";
                if (env != null)
                {
                    foreach (var e in env)
                    {
                        envParam += $" {e.Key}={e.Value}";
                    }
                }

                _shellRunner.RunShell($"chroot rootfs /usr/bin/env {envParam} bash -c \"{command}\"", new RunnerOptions
                {
                    UseSudo = true,
                    Env = env,
                    WorkingDirectory = _rootfsCacheSession.Dir
                });
            }
        }
    }
}