using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using AptRepoBuilder.Apt;
using AptRepoBuilder.BuildCache;
using AptRepoBuilder.Config;
using AptRepoBuilder.Git;
using AptRepoBuilder.Rootfs;
using AptRepoBuilder.Shell;
using Mono.Unix.Native;
using Newtonsoft.Json;
using Serilog;

namespace AptRepoBuilder.Workspace.Impl
{
    public class Component : IComponent
    {
        private readonly ComponentConfig _componentConfig;
        private readonly IGitCache _gitCache;
        private readonly Workspace _workspace;
        private readonly IBuildCache _buildCache;
        private readonly IShellRunner _shellRunner;
        private readonly IRootfsExecutor _rootfsExecutor;
        private readonly IAptHelper _aptHelper;
        private readonly RootConfig _rootConfig;

        public Component(string name,
            ComponentConfig componentConfig,
            IGitCache gitCache,
            Workspace workspace,
            IBuildCache buildCache,
            IShellRunner shellRunner,
            IRootfsExecutor rootfsExecutor,
            IAptHelper aptHelper,
            RootConfig rootConfig)
        {
            _componentConfig = componentConfig;
            _gitCache = gitCache;
            _workspace = workspace;
            _buildCache = buildCache;
            _shellRunner = shellRunner;
            _rootfsExecutor = rootfsExecutor;
            _aptHelper = aptHelper;
            _rootConfig = rootConfig;
            name.NotNullOrEmpty(nameof(name));
            
            Name = name;
            Dependencies = (_componentConfig.Dependencies ?? new List<string>()).AsReadOnly();
            if (_componentConfig.Source.Commit == "latest")
            {
                SourceRev = new ComponentSrcRevLatest();
            }
            else
            {
                SourceRev = new ComponentSrcRevSpecific(_componentConfig.Source.Commit);
            }
        }

        public string Name { get; }

        public string MD5 { get; private set; } = null;
        
        public ReadOnlyCollection<string> Dependencies { get; }

        public string GitUrl => _componentConfig.Source.Url;

        public string Branch => _componentConfig.Source.Branch;

        public ComponentSrcRev SourceRev { get; private set; }
        
        public void ResolveUnknownCommit()
        {
            if (SourceRev.Type == ComponentSrcRevType.Latest)
            {
                Log.Information("Fetching latest commit for {component}...", Name);
                var commit = _gitCache.GetLatestCommit(GitUrl, Branch);
                SourceRev = new ComponentSrcRevSpecific(commit);
            }
        }

        public void FetchSources()
        {
            ResolveUnknownCommit();
            Log.Information("Fetching sources for {component}...", Name);
            _gitCache.Fetch(GitUrl);
            if (!_gitCache.ContainsBranchAndCommit(GitUrl, Branch, SourceRev.Commit))
            {
                throw new AptRepoToolException($"Commit {SourceRev.Commit.Quoted()} not found in branch {Branch.Quoted()} for component {Name.Quoted()}.");
            }
        }

        public void ExtractSource(string directory)
        {
            _gitCache.Extract(GitUrl, Branch, SourceRev.Commit, directory);
        }

        public void ExtractPackages(string directory)
        {
            var packageCacheKey = $"packages-{Name}-{MD5}";
            if (!_buildCache.HasCacheDirectory(packageCacheKey))
            {
                throw new AptRepoToolException($"Can't extract packages for {Name.Quoted()}, none are available.");
            }

            var packageCacheDirectory = _buildCache.GetCacheDirectory(packageCacheKey);
            Log.Information("Extracting packages for {component}...", Name);
            _shellRunner.RunShell($"cp -rp {Path.Combine(packageCacheDirectory, "*")} {directory}");
        }

        public void CheckCache(string directory)
        {
            var packageCacheKey = $"packages-{Name}-{MD5}";
            if (_buildCache.HasCacheDirectory(packageCacheKey))
            {
                // No need to check cache, we already have it locally.
                return;
            }
            
            var cacheFile = Path.Combine(directory, $"{packageCacheKey}.tar.gz");
            if (File.Exists(cacheFile))
            {
                Log.Information($"Restoring {Name.Quoted()} from cache...");
                using (var buildCache = _buildCache.StartSession(packageCacheKey, true))
                {
                    _shellRunner.RunShell($"tar -xf {cacheFile.Quoted()} -C {buildCache.Dir.Quoted()}");
                    buildCache.Commit();
                }
                Log.Information($"Sucessfully restored {Name.Quoted()} from cache.");
            }
        }
        
        public void PublishCache(string directory)
        {
            Log.Information($"Publishing {Name.Quoted()} into the cache...");
            
            CalculateMD5Sum();
            var packageCacheKey = $"packages-{Name}-{MD5}";
            
            // Check if it is already cached.
            var destinationCacheFile = Path.Combine(directory, $"{packageCacheKey}.tar.gz");
            if (File.Exists(destinationCacheFile))
            {
                Log.Information($"The package {Name.Quoted()} is already cached.");
                return;
            }
            
            // Check if we have it built locally.
            if (!_buildCache.HasCacheDirectory(packageCacheKey))
            {
                throw new AptRepoToolException($"The package {Name.Quoted()}, hasn't been built, can't publish cache.");
            }
            
            // Let's package up this directory and move it to the cache.
            var destinationCacheFileTmp = $"{destinationCacheFile}.tmp";
            if (File.Exists(destinationCacheFileTmp))
            {
                File.Delete(destinationCacheFileTmp);
            }
            _shellRunner.RunShell($"tar -C {_buildCache.GetCacheDirectory(packageCacheKey).Quoted()} -czf {destinationCacheFileTmp.Quoted()} .");
            File.Move(destinationCacheFileTmp, destinationCacheFile);
        }

        public string GetPackagesDirectory()
        {
            var packageCacheKey = $"packages-{Name}-{MD5}";
            if (!_buildCache.HasCacheDirectory(packageCacheKey))
            {
                throw new AptRepoToolException($"Packages for {Name.Quoted()} are not available.");
            }
            return _buildCache.GetCacheDirectory(packageCacheKey);
        }
        
        public void CalculateMD5Sum()
        {
            if (!string.IsNullOrEmpty(MD5))
            {
                return;
            }

            if (SourceRev.Type == ComponentSrcRevType.Latest)
            {
                ResolveUnknownCommit();
            }

            var steps = JsonConvert.SerializeObject(_componentConfig.Steps ?? new List<ComponentConfig.Step>());
            
            var hash = $"{SourceRev.Commit}{GitUrl}{steps.CalculateMD5Hash()}";
            if (_rootConfig.Rootfs.RebuildComponentsOnChange)
            {
                hash += $"-{_rootfsExecutor.MD5Sum}";
            }
            foreach (var dependency in Dependencies)
            {
                var component = _workspace.GetComponent(dependency);
                component.CalculateMD5Sum();
                hash += component.MD5;
            }

            MD5 = hash.CalculateMD5Hash();
        }
        
        public void Build(bool force, bool bashPrompt)
        {
            Log.Information("Building {component}...", Name);

            // Ensure all commits are resolved, and all dependencies have calculated their MD5.
            CalculateMD5Sum();
            
            if (!string.IsNullOrEmpty(_workspace.CacheDirectory))
            {
                CheckCache(_workspace.CacheDirectory);
            }
            
            if (_buildCache.HasCacheDirectory($"packages-{Name}-{MD5}"))
            {
                // This component was already built, and it's outputs are available.
                if (force)
                {
                    Log.Warning("Forcing a rebuild of {component}...", Name);
                }
                else
                {
                    Log.Information("Skipping {component}, it is already built.", Name);
                    return;
                }
            }
            
            // Make sure we have all the sources.
            FetchSources();

            var buildDirectory = _buildCache.StartSession($"build-{Name}", false);
            
            // Prepare the git directory
            var gitDirectory = Path.Combine(buildDirectory.Dir, "git");
            gitDirectory.CleanOrCreateDirectory();
            
            // Checkout the code.
            ExtractSource(gitDirectory);
            
            // Prepare out build directory
            var buildWorkingDirectory = Path.Combine(buildDirectory.Dir, "build");
            buildWorkingDirectory.CleanOrCreateDirectory();
            
            // Prepare the packages directory
            var packagesDirectory = Path.Combine(buildDirectory.Dir, "packages");
            packagesDirectory.CleanOrCreateDirectory();
            
            var userId = int.Parse(_shellRunner.ReadShell("id -u"));
            
            var scripts = new Dictionary<string, StringBuilder>();

            {
                var prepScript = scripts["prepare.sh"] = new StringBuilder();
                prepScript.AppendLine("#!/usr/bin/env bash");
                prepScript.AppendLine("set -e");
                prepScript.AppendLine("export LANG=C");
                prepScript.AppendLine("export LC_ALL=C");
                if (userId != 0)
                {
                    prepScript.AppendLine($"useradd -u {userId} dummy");
                    prepScript.AppendLine("mkdir -p /home/dummy/.ssh");
                    prepScript.AppendLine("chown -R dummy:dummy /home/dummy");
                    prepScript.AppendLine("echo \"dummy ALL=(ALL) NOPASSWD: ALL\" > /etc/sudoers.d/dummy");
                }

                prepScript.AppendLine("apt-get update");
            }

            var entryScript = scripts["entry.sh"] = new StringBuilder();
            entryScript.AppendLine("#!/usr/bin/env bash");
            entryScript.AppendLine("set -e");
            entryScript.AppendLine("export LANG=C");
            entryScript.AppendLine("export LC_ALL=C");
            entryScript.AppendLine("cd /workspace/scripts");
            
            if (_componentConfig.Steps != null)
            {
                var number = 1;
                foreach (var step in _componentConfig.Steps)
                {
                    var stepScriptName = $"script{number++}.sh";
                    var stepScript = scripts[stepScriptName] = new StringBuilder();
                    stepScript.AppendLine("#!/usr/bin/env bash");
                    stepScript.AppendLine("set -e");
                    entryScript.AppendLine($"./{stepScriptName}");

                    if (step is ComponentConfig.MakeOrigStep makeOrigStep)
                    {
                        var targetDirectory = "/workspace/git";
                        if (!string.IsNullOrEmpty(makeOrigStep.Folder))
                        {
                            if (Path.IsPathRooted(makeOrigStep.Folder))
                            {
                                throw new AptRepoToolException($"Invalid {"folder".Quoted()} field.");
                            }

                            targetDirectory = Path.Combine(targetDirectory, makeOrigStep.Folder);
                        }
                        
                        stepScript.AppendLine(". /usr/lib/pbuilder/pbuilder-buildpackage-funcs");
                        stepScript.AppendLine($"cd {targetDirectory.Quoted()}");
                        stepScript.AppendLine("export VERSION=$(dpkg-parsechangelog -l debian/changelog -S Version)");
                        stepScript.AppendLine("export SOURCE=$(dpkg-parsechangelog -l debian/changelog -S Source)");
                        stepScript.AppendLine("export VERSION_BASE=$(perl -e \"use Dpkg::Version; printf Dpkg::Version->new(\\\"${VERSION}\\\")->as_string(omit_epoch => 1, omit_revision => 1);\")");
                        stepScript.AppendLine("export SOURCE_TARBALL_PATH=\"../${SOURCE}_${VERSION_BASE}.orig.tar.xz\"");
                        stepScript.AppendLine("export DSC_FILE_NAME=\"${SOURCE}_${VERSION}.dsc\"");
                        stepScript.AppendLine("export CHANGES_ARCHITECTURE=$(dpkg-architecture -qDEB_HOST_ARCH)");
                        stepScript.AppendLine("tar cfJ ${SOURCE_TARBALL_PATH} --exclude=\"./debian*\" . --transform \"s,^,${SOURCE}_${VERSION_BASE}/,\"");
                    }
                    else if (step is ComponentConfig.SourceBuildStep sourceBuildStep)
                    {
                        var targetDirectory = "/workspace/git";
                        if (!string.IsNullOrEmpty(sourceBuildStep.Folder))
                        {
                            if (Path.IsPathRooted(sourceBuildStep.Folder))
                            {
                                throw new AptRepoToolException($"Invalid {"folder".Quoted()} field.");
                            }

                            targetDirectory = Path.Combine(targetDirectory, sourceBuildStep.Folder);
                        }
                        
                        stepScript.AppendLine(". /usr/lib/pbuilder/pbuilder-buildpackage-funcs");
                        stepScript.AppendLine($"cd {targetDirectory.Quoted()}");
                        stepScript.AppendLine("export VERSION=$(dpkg-parsechangelog -l debian/changelog -S Version)");
                        stepScript.AppendLine("export SOURCE=$(dpkg-parsechangelog -l debian/changelog -S Source)");
                        stepScript.AppendLine("dpkg-source -b .");
                        stepScript.AppendLine("copydsc ../${SOURCE}_${VERSION}.dsc ${ARB_BUILD_DIR}");
                    }
                    else if (step is ComponentConfig.BashStep bashStep)
                    {
                        var targetDirectory = "/workspace/git";
                        stepScript.AppendLine($"cd {targetDirectory.Quoted()}");
                        stepScript.AppendLine(bashStep.Script);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
            }

            // Build the script that will build the packages.
            {
                entryScript.AppendLine("./build.sh");

                var stepScript = scripts["build.sh"] = new StringBuilder();
                stepScript.AppendLine("#!/usr/bin/env bash");
                stepScript.AppendLine("set -e");
                stepScript.AppendLine(". /usr/lib/pbuilder/pbuilder-buildpackage-funcs");
                
                // Find the path to the dsc file.
                stepScript.AppendLine("export CHANGES_ARCHITECTURE=$(dpkg-architecture -qDEB_HOST_ARCH)");
                stepScript.AppendLine("cd ${ARB_BUILD_DIR}");
                stepScript.AppendLine("DSC_FILES=(*.dsc)");
                stepScript.AppendLine("DSC_FILE=${DSC_FILES[0]}");
                stepScript.AppendLine("if [ ! -f ${dsc_file} ]; then");
                stepScript.AppendLine("  echo \"There was no .dsc file found in ARB_BUILD_DIR\"");
                stepScript.AppendLine("fi");
                
                // Build the package.
                stepScript.AppendLine("dpkg-source -x ${DSC_FILE} working");
                stepScript.AppendLine("cd working");
                stepScript.AppendLine("sudo /usr/lib/pbuilder/pbuilder-satisfydepends");
                stepScript.AppendLine("fakeroot dpkg-buildpackage -us -uc");
                
                // Copy the build outputs to the packages directory.
                stepScript.AppendLine("CHANGES_BASENAME=$(dsc_get_basename \"../${DSC_FILE}\" \"yes\")");
                stepScript.AppendLine("FILES=$(get822files \"changes\" \"../${CHANGES_BASENAME}_${CHANGES_ARCHITECTURE}.changes\")");
                stepScript.AppendLine("for FILE in $FILES; do");
                stepScript.AppendLine("  if [ -f \"${FILE}\" ]; then");
                stepScript.AppendLine("    cp -p \"${FILE}\" \"${ARB_PACKAGES_DIR}\"");
                stepScript.AppendLine("  fi");
                stepScript.AppendLine("done");
            }

            var scriptDirectory = Path.Combine(buildDirectory.Dir, "scripts");
            scriptDirectory.CleanOrCreateDirectory();
            foreach (var script in scripts)
            {
                var scriptPath = Path.Combine(scriptDirectory, script.Key);
                File.WriteAllText(scriptPath, script.Value.ToString());
                Syscall.chmod(scriptPath, FilePermissions.ACCESSPERMS);
            }

            var mountedVolumes = new List<MountedVolume>
            {
                new MountedVolume
                {
                    Source = buildWorkingDirectory,
                    Target = "/workspace/build"
                },
                new MountedVolume
                {
                    Source = scriptDirectory,
                    Target = "/workspace/scripts"
                },
                new MountedVolume
                {
                    Source = gitDirectory,
                    Target = "/workspace/git"
                },
                new MountedVolume
                {
                    Source = packagesDirectory,
                    Target = "/workspace/packages"
                }
            };
            
            // Find all the dependent components so that we
            // can mount there packages as an apt repo.
            var dependentComponents = _workspace.GetComponentDependencies(Name);
            if (dependentComponents.Count > 0)
            {
                Log.Information("Preparing the dependency apt repos.");
                
                var sourceListData = new StringBuilder();
                foreach (var dependentComponent in dependentComponents)
                {
                    var sourcePackageRepo = dependentComponent.GetPackagesDirectory();
                    var targetPackageRepo = $"/workspace/repo/{dependentComponent.Name}";
                    mountedVolumes.Add(new MountedVolume
                    {
                        Source = sourcePackageRepo,
                        Target = targetPackageRepo
                    });
                    sourceListData.AppendLine($"deb [trusted=yes] file:{targetPackageRepo} ./");
                }
                
                // Right the apt repo source file
                var aptSourceConfig = Path.Combine(buildDirectory.Dir, "dependencies.list");
                if (File.Exists(aptSourceConfig))
                {
                    File.Delete(aptSourceConfig);
                }
                File.WriteAllText(aptSourceConfig, sourceListData.ToString());
                
                // Mount the dependencies.list file inside the rootfs
                mountedVolumes.Add(new MountedVolume
                {
                    Source = aptSourceConfig,
                    Target = "/etc/apt/sources.list.d/dependencies.list"
                });
            }
            using (var rootfsSession = _rootfsExecutor.StartSession(new RunOptions
            {
                Mounts = mountedVolumes
            }))
            {
                Log.Information("Preparing the rootfs environment...");
                rootfsSession.Run("/workspace/scripts/prepare.sh");

                rootfsSession.Run("chmod +777 /workspace");
                
                var env = new Dictionary<string, string>
                {
                    {"ARB_BUILD_DIR", "/workspace/build"},
                    {"ARB_SCRIPTS_DIR", "/workspace/scripts"},
                    {"ARB_GIT_DIR", "/workspace/git"},
                    {"ARB_PACKAGES_DIR", "/workspace/packages"}
                };
                
                var containerEntry = "/workspace/scripts/entry.sh";
                if (bashPrompt)
                {
                    // We want to start the container, but give the running user
                    // a bash prompt to poke around before the scripts are run.
                    Log.Warning("Starting a bash prompt before building {component}.", Name);
                    Log.Warning("The script {script} will be ran after exiting the prompt.", containerEntry);
                    rootfsSession.Run(userId == 0 ? "bash" : "su dummy -c bash", env);
                }
                
                Log.Logger.Information("Running build steps...");
                rootfsSession.Run(userId == 0 ? $"{containerEntry}" : $"su dummy -c \"{containerEntry}\"", env);
            }
            
            using (var packagesCache = _buildCache.StartSession($"packages-{Name}-{MD5}", true))
            {
                if (Directory.GetFiles(packagesDirectory).Length == 0)
                {
                    throw new AptRepoToolException($"The component {Name} didn't generate any packages.");
                }
                _shellRunner.RunShell($"cp -r {Path.Combine(packagesDirectory, "*")} . ", 
                    new RunnerOptions
                    {
                        WorkingDirectory = packagesCache.Dir
                    });
                _aptHelper.ScanSourcesAndPackages(packagesCache.Dir);
                packagesCache.Commit();
            }
        }
    }
}