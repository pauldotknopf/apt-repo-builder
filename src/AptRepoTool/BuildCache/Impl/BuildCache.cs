using System.IO;
using Microsoft.Extensions.Options;

namespace AptRepoTool.BuildCache.Impl
{
    public class BuildCache : IBuildCache
    {
        private readonly BuildCacheOptions _options;

        public BuildCache(IOptions<BuildCacheOptions> options)
        {
            _options = options.Value;
        }
        
        public ICacheSession StartSession(string key, bool transactional)
        {
            var directory = GetCacheDirectory(key);
            if (transactional)
            {
                var directoryTmp = $"{directory}-tmp";
                if (Directory.Exists(directoryTmp))
                {
                    Directory.Delete(directoryTmp, true);
                }
                Directory.CreateDirectory(directoryTmp);
                return new TransactionalCacheSession(directoryTmp, directory);
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return new NonTransactionalCacheSession(directory);
        }

        public string GetCacheDirectory(string key)
        {
            return Path.Combine(_options.BuildCacheDir, key);
        }

        public bool HasCacheDirectory(string key)
        {
            return Directory.Exists(GetCacheDirectory(key));
        }

        class NonTransactionalCacheSession : ICacheSession
        {
            public NonTransactionalCacheSession(string directory)
            {
                Dir = directory;
            }
            
            public string Dir { get; }
            
            public void Commit()
            {
                // Nothing to commit.
            }

            public void Clean()
            {
                foreach (var dir in Directory.GetDirectories(Dir, "*", SearchOption.TopDirectoryOnly))
                {
                    Directory.Delete(dir, true);
                }

                foreach (var file in Directory.GetFiles(Dir, "*", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(file);
                }
            }

            public void Dispose()
            {
                // Nothing to do
            }
        }
        
        class TransactionalCacheSession : ICacheSession
        {
            private string _directory;
            private readonly string _finalDirectory;
            private bool _commited;

            public TransactionalCacheSession(string directory, string finalDirectory)
            {
                _directory = directory;
                _finalDirectory = finalDirectory;
            }

            public string Dir => _directory;

            public void Commit()
            {
                if (_commited)
                {
                    return;
                }

                if (Directory.Exists(_finalDirectory))
                {
                    Directory.Delete(_finalDirectory, true);
                }
                
                Directory.Move(_directory, _finalDirectory);
                _directory = _finalDirectory;

                _commited = true;
            }

            public void Clean()
            {
                foreach (var dir in Directory.GetDirectories(_directory, "*", SearchOption.TopDirectoryOnly))
                {
                    Directory.Delete(dir, true);
                }

                foreach (var file in Directory.GetFiles(_directory, "*", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(file);
                }
            }

            public void Dispose()
            {
                // If we didn't commit this directory, delete the temp directory we were working in.
                if (!_commited)
                {
                    Directory.Delete(_directory, true);
                }
            }
        }
    }
}