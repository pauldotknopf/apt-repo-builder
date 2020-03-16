using System;
using System.Collections.Generic;

namespace AptRepoTool.Config
{
    public class ComponentConfig
    {
        public SourceConfig Source { get; set; }
        
        public class SourceConfig
        {
            public string Url { get; set; }
            
            public string Branch { get; set; }
            
            public string Commit { get; set; }
        }
        
        public List<string> Dependencies { get; set; }

        public List<Step> Steps { get; set; }
        
        public abstract class Step
        {
            public abstract string Type { get; }
        }

        /// <summary>
        /// This type will indicate that the workspace we are looking at represents
        /// a repo that has source code, along with a "debian" folder.
        /// We will do a few things.
        /// 1. Generate the source tarball.
        /// 2. Build a source package.
        /// 3. Build the binaries.
        /// 4. Copy them to the COMPONENT_OUTPUT_DIR directory.
        /// </summary>
        public class DebianizedBuildStep : Step
        {
            public override string Type => "debianized-build";

            public string Folder { get; set; }
        }

        /// <summary>
        /// This will simply run a bash script.
        /// </summary>
        public class BashStep : Step
        {
            public override string Type => "bash";
            
            public string Script { get; set; }
        }
    }
}