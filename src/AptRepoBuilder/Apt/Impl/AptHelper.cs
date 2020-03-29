using AptRepoBuilder.Shell;

namespace AptRepoBuilder.Apt.Impl
{
    public class AptHelper : IAptHelper
    {
        private readonly IShellRunner _shellRunner;

        public AptHelper(IShellRunner shellRunner)
        {
            _shellRunner = shellRunner;
        }
        
        public void ScanSourcesAndPackages(string directory)
        {
            _shellRunner.RunShell("dpkg-scanpackages . | gzip -9c > Packages.gz " +
                                  "&& dpkg-scansources . | gzip -9c > Sources.gz", new RunnerOptions
            {
                WorkingDirectory = directory
            });
        }
    }
}