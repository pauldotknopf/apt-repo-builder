using System;

namespace AptRepoTool
{
    public class AptRepoToolException : Exception
    {
        public AptRepoToolException(string message) : base(message)
        {
            
        }
        
        public AptRepoToolException(string message, Exception inner) : base(message, inner)
        {
            
        }
    }
}