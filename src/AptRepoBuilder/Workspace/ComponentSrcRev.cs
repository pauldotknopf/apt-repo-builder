using System;

namespace AptRepoBuilder.Workspace
{
    public abstract class ComponentSrcRev
    {
        public abstract ComponentSrcRevType Type { get; }

        public string Commit
        {
            get
            {
                if (this is ComponentSrcRevSpecific componentSrcRevSpecific)
                {
                    return componentSrcRevSpecific.Revision;
                }
                throw new Exception("No commit resolved.");
            }
        }
    }

    public enum ComponentSrcRevType
    {
        Latest,
        Revision
    }

    public class ComponentSrcRevLatest : ComponentSrcRev
    {
        public override ComponentSrcRevType Type => ComponentSrcRevType.Latest;
    }

    public class ComponentSrcRevSpecific : ComponentSrcRev
    {
        public ComponentSrcRevSpecific(string revision)
        {
            Revision = revision;
        }
        
        public override ComponentSrcRevType Type => ComponentSrcRevType.Revision;
        
        public string Revision { get; }
    }
}