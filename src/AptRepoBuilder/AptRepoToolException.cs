using System;

namespace AptRepoBuilder
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