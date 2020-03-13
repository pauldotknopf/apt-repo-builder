using System;

namespace AptRepoTool
{
    public static class GuardExtensions
    {
        public static void NotNullOrEmpty(this string value, string name)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"The parameter {name.Quoted()} was null or empty.");
            }
        }
    }
}