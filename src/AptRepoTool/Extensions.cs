using System.Text;

namespace AptRepoTool
{
    public static class Extensions
    {
        public static string Quoted(this string val)
        {
            return $"\"{val}\"";
        }
        
        public static string CalculateMD5Hash(this string input)
        {
            var md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);
 
            var sb = new StringBuilder();
            foreach (var t in hash)
            {
                sb.Append(t.ToString("X2"));
            }
            return sb.ToString();
        }
    }
}