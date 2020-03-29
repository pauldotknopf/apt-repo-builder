namespace AptRepoBuilder.Workspace
{
    public class ComponentBuildOptions
    {
        public bool ForceBuild { get; set; }
        
        public bool ForceBuildDependencies { get; set; }
        
        public bool PromptBeforeBuild { get; set; }
    }
}