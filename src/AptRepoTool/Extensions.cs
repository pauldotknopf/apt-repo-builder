namespace AptRepoTool
{
    public static class Extensions
    {
        public static string Quoted(this string val)
        {
            return $"\"{val}\"";
        }
    }
}