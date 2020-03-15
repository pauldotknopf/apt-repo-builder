namespace AptRepoTool.Config
{
    public interface IConfigParser
    {
        RootConfig LoadRootConfig(string yaml);

        RootfsConfig LoadRootfsConfig(string yaml);

        ComponentConfig LoadComponentConfig(string yaml);
    }
}