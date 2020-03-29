using System.Collections.Generic;

namespace AptRepoBuilder.Config
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
        /// The step will create an "orig" tar ball.
        /// This is needed when using "3.0 (quilt)",
        /// which requires source builds to have an
        /// orig.tar.xz
        /// </summary>
        public class MakeOrigStep : Step
        {
            public override string Type => "make-orig";

            public string Folder { get; set; }
        }
        
        /// <summary>
        /// This type will look for a dsc file, and extract/build it.
        /// </summary>
        public class SourceBuildStep : Step
        {
            public override string Type => "source-build";
            
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