using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using AptRepoTool.Apt;
using AptRepoTool.BuildCache;
using AptRepoTool.Config;
using AptRepoTool.Git;
using AptRepoTool.Rootfs;
using AptRepoTool.Shell;
using Mono.Unix.Native;
using Serilog;

namespace AptRepoTool.Workspace.Impl
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

        public Component(string name,
            ComponentConfig componentConfig,
            IGitCache gitCache,
            Workspace workspace,
            IBuildCache buildCache,
            IShellRunner shellRunner,
            IRootfsExecutor rootfsExecutor,
            IAptHelper aptHelper)
        {
            _componentConfig = componentConfig;
            _gitCache = gitCache;
            _workspace = workspace;
            _buildCache = buildCache;
            _shellRunner = shellRunner;
            _rootfsExecutor = rootfsExecutor;
            _aptHelper = aptHelper;
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
            
            var hash = $"{SourceRev.Commit}{GitUrl}{_rootfsExecutor.MD5Sum}";
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
            
            // Prepare the scripts directory (that will be ran in the image).
            var scripts = new Dictionary<string, StringBuilder>();
            if (_componentConfig.Steps != null)
            {
                // Switch to another user.
                var userId = int.Parse(_shellRunner.ReadShell("id -u"));
                var prepScript = scripts["prepare.sh"] = new StringBuilder();
                prepScript.AppendLine("#!/usr/bin/env bash");
                prepScript.AppendLine("set -e");
                prepScript.AppendLine("export LANG=C");
                prepScript.AppendLine("export LC_ALL=C");
                prepScript.AppendLine($"useradd -u {userId} dummy");
                prepScript.AppendLine("mkdir -p /home/dummy/.ssh");
                prepScript.AppendLine("chown -R dummy:dummy /home/dummy");
                prepScript.AppendLine("echo \"dummy ALL=(ALL) NOPASSWD: ALL\" > /etc/sudoers.d/dummy");
                prepScript.AppendLine("apt-get update");
                
                var entryScript = scripts["entry.sh"] = new StringBuilder();
                entryScript.AppendLine("#!/usr/bin/env bash");
                entryScript.AppendLine("set -e");
                entryScript.AppendLine("export LANG=C");
                entryScript.AppendLine("export LC_ALL=C");
                
                entryScript.AppendLine("cd /workspace/scripts");
                var number = 1;
                foreach (var step in _componentConfig.Steps)
                {
                    var stepScriptName = $"script{number++}.sh";
                    var stepScript = scripts[stepScriptName] = new StringBuilder();
                    stepScript.AppendLine("#!/usr/bin/env bash");
                    stepScript.AppendLine("set -e");
                    entryScript.AppendLine($"./{stepScriptName}");
                    if (step is ComponentConfig.DebianizedBuildStep debianizedBuildStep)
                    {
                        var targetDirectory = "/workspace/git";
                        if (!string.IsNullOrEmpty(debianizedBuildStep.Folder))
                        {
                            if (Path.IsPathRooted(debianizedBuildStep.Folder))
                            {
                                throw new AptRepoToolException($"Invalid {"folder".Quoted()} field.");
                            }
                            targetDirectory = Path.Combine(targetDirectory, debianizedBuildStep.Folder);
                        }
                        
                        // Build the source package
                        stepScript.AppendLine(". /usr/lib/pbuilder/pbuilder-buildpackage-funcs");
                        stepScript.AppendLine($"cd {targetDirectory.Quoted()}");
                        stepScript.AppendLine("export VERSION=$(dpkg-parsechangelog -l debian/changelog -S Version)");
                        stepScript.AppendLine("export SOURCE=$(dpkg-parsechangelog -l debian/changelog -S Source)");
                        stepScript.AppendLine("export VERSION_BASE=$(perl -e \"use Dpkg::Version; printf Dpkg::Version->new(\\\"${VERSION}\\\")->as_string(omit_epoch => 1, omit_revision => 1);\")");
                        stepScript.AppendLine("export SOURCE_TARBALL_NAME=\"${SOURCE}_${VERSION_BASE}.orig.tar.xz\"");
                        stepScript.AppendLine("export DSC_FILE_NAME=\"${SOURCE}_${VERSION}.dsc\"");
                        stepScript.AppendLine("export CHANGES_ARCHITECTURE=$(dpkg-architecture -qDEB_HOST_ARCH)");
                        stepScript.AppendLine("if [ ! -f \"../${SOURCE_TARBALL_NAME}\" ]; then");
                        stepScript.AppendLine("  tar cfJ ../${SOURCE_TARBALL_NAME} --exclude=\"./debian*\" . --transform \"s,^,${SOURCE}_${VERSION_BASE}/,\"");
                        stepScript.AppendLine("fi");
                        stepScript.AppendLine("dpkg-source -b .");
                        
                        // Now, let's build that source package.
                        stepScript.AppendLine("copydsc ../${SOURCE}_${VERSION}.dsc ${ARB_BUILD_DIR}");
                        stepScript.AppendLine("cd ${ARB_BUILD_DIR}");
                        stepScript.AppendLine("dpkg-source -x ${DSC_FILE_NAME} working");
                        stepScript.AppendLine("cd working");
                        stepScript.AppendLine("sudo /usr/lib/pbuilder/pbuilder-satisfydepends");
                        stepScript.AppendLine("fakeroot dpkg-buildpackage -us -uc");
                        
                        // Copy the build outputs to the packages directory.
                        stepScript.AppendLine("CHANGES_BASENAME=$(dsc_get_basename \"../${DSC_FILE_NAME}\" \"yes\")");
                        stepScript.AppendLine("FILES=$(get822files \"changes\" \"../${CHANGES_BASENAME}_${CHANGES_ARCHITECTURE}.changes\")");
                        stepScript.AppendLine("for FILE in $FILES; do");
                        stepScript.AppendLine("  if [ -f \"${FILE}\" ]; then");
                        stepScript.AppendLine("    cp -p \"${FILE}\" \"${ARB_PACKAGES_DIR}\"");
                        stepScript.AppendLine("  fi");
                        stepScript.AppendLine("done");
                    }
                    else if (step is ComponentConfig.BashStep bashStep)
                    {
                        var targetDirectory = "/workspace/git";
                        stepScript.AppendLine($"cd {targetDirectory.Quoted()}");
                        stepScript.AppendLine(bashStep.Script);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            var scriptDirectory = Path.Combine(buildDirectory.Dir, "scripts");
            scriptDirectory.CleanOrCreateDirectory();
            foreach (var script in scripts)
            {
                var scriptPath = Path.Combine(scriptDirectory, script.Key);
                File.WriteAllText(scriptPath, script.Value.ToString());
                Syscall.chmod(scriptPath, FilePermissions.ACCESSPERMS);
            }

            using (var rootfsSession = _rootfsExecutor.StartSession(new RunOptions
            {
                Mounts =
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
                }
            }))
            {
                Log.Information("Preparing the rootfs environment...");
                rootfsSession.Run("/workspace/scripts/prepare.sh");

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
                    rootfsSession.Run("su dummy -c bash", env);
                }
                
                Log.Logger.Information("Running build steps...");
                rootfsSession.Run($"su dummy -c \"{containerEntry}\"", env);
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