using System;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace CapNet.Storage
{
    /// <summary>Single-process in-memory backing for <see cref="ICapStateStore"/>. Local-dev and demos only.</summary>
    public sealed class MemoryCapStateStore : ICapStateStore
    {
        private readonly MemoryCache _cache;

        public MemoryCapStateStore(string name = "CapNet.State")
        {
            _cache = new MemoryCache(name);
        }

        public Task PutAsync(string key, string value, TimeSpan ttl)
        {
            var policy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.UtcNow.Add(ttl) };
            _cache.Set(key, value ?? string.Empty, policy);
            return Task.CompletedTask;
        }

        public Task<string> GetAsync(string key) => Task.FromResult(_cache.Get(key) as string);

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
