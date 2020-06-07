using System.Collections.Generic;

namespace AptRepoBuilder.Config
{
    public interface IConfigParser
    {
        RootConfig LoadRootConfig(string yaml);

        RootfsConfig LoadRootfsConfig(string yaml);

        ComponentConfig LoadComponentConfig(string yaml);

        List<SourceOverrideConfig> LoadSourceOverrides(string yaml);
    }
}