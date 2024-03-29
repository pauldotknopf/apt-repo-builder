namespace AptRepoBuilder.BuildCache
{
    public interface IBuildCache
    {
        ICacheSession StartSession(string key, bool transactional);

        string GetCacheDirectory(string key);
        
        bool HasCacheDirectory(string key);
    }
}