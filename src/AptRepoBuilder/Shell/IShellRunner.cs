namespace AptRepoBuilder.Shell
{
    public interface IShellRunner
    {
        void RunShell(string command, RunnerOptions runnerOptions = null);
        
        string ReadShell(string command, RunnerOptions runnerOptions = null);
    }
}