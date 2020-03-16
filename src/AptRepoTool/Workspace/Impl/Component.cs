using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
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

        public Component(string name,
            ComponentConfig componentConfig,
            IGitCache gitCache,
            Workspace workspace,
            IBuildCache buildCache,
            IShellRunner shellRunner,
            IRootfsExecutor rootfsExecutor)
        {
            _componentConfig = componentConfig;
            _gitCache = gitCache;
            _workspace = workspace;
            _buildCache = buildCache;
            _shellRunner = shellRunner;
            _rootfsExecutor = rootfsExecutor;
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

        public void Extract(string directory)
        {
            _gitCache.Extract(GitUrl, Branch, SourceRev.Commit, directory);
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
            Extract(gitDirectory);
            
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
                        stepScript.AppendLine("VERSION=$(dpkg-parsechangelog -l debian/changelog -S Version)");
                        stepScript.AppendLine("SOURCE=$(dpkg-parsechangelog -l debian/changelog -S Source)");
                        stepScript.AppendLine("VERSION_BASE=$(perl -e \"use Dpkg::Version; printf Dpkg::Version->new(\\\"${VERSION}\\\")->as_string(omit_epoch => 1, omit_revision => 1);\")");
                        stepScript.AppendLine("SOURCE_TARBALL=\"../${SOURCE}_${VERSION_BASE}.orig.tar.xz\"");
                        stepScript.AppendLine("DSC_FILE_NAME=\"${SOURCE}_${VERSION}.dsc\"");
                        stepScript.AppendLine("CHANGES_ARCHITECTURE=$(dpkg-architecture -qDEB_HOST_ARCH)");
                        stepScript.AppendLine("CHANGES_BASENAME=$(dsc_get_basename \"${DSC_FILE_NAME}\" \"yes\")");
                        stepScript.AppendLine("if [ ! -f \"$SOURCE_TARBALL\" ]; then");
                        stepScript.AppendLine("  echo \"TODO: Auto generate the source tarball!\"");
                        stepScript.AppendLine("  exit 1");
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
            
            // Pass our scripts and source to the executor to be ran.
            var containerEntry = "/workspace/scripts/entry.sh";
            var interactive = false;
            if (bashPrompt)
            {
                // We want to start the container, but give the running user
                // a bash prompt to poke around before the scripts are run.
                Log.Warning("Starting a bash prompt before building {component}.", Name);
                Log.Warning("The script {script} will be ran after exiting the prompt.", containerEntry);
                containerEntry = $"bash && {containerEntry}";
                interactive = true;
            }
            _rootfsExecutor.Run(containerEntry, new RunOptions
            {
                Interactive = interactive,
                Env =
                {
                    { "ARB_BUILD_DIR" , "/workspace/build" },
                    { "ARB_SCRIPTS_DIR", "/workspace/scripts" },
                    { "ARB_GIT_DIR", "/workspace/git" },
                    { "ARB_PACKAGES_DIR", "/workspace/packages" }
                },
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
            });
            
            using (var packagesCache = _buildCache.StartSession($"packages-{Name}-{MD5}", true))
            {
                if (Directory.GetFiles(packagesDirectory).Length > 0 ||
                    Directory.GetDirectories(packagesDirectory).Length > 0)
                {
                    _shellRunner.RunShell($"cp -r {Path.Combine(packagesDirectory, "*")} .", new RunnerOptions
                    {
                        WorkingDirectory = packagesCache.Dir
                    });
                }
                packagesCache.Commit();
            }
        }
    }
}