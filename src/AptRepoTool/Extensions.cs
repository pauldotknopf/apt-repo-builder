using System.IO;
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
            return sb.ToString().ToLower();
        }

        public static void CleanOrCreateDirectory(this string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                return;
            }
            foreach (var child in Directory.GetDirectories(directory))
            {
                Directory.Delete(child, true);
            }
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
            }
        }
    }
}