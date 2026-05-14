using System;
using System.Threading.Tasks;

namespace CapNet.Storage
{
    /// <summary>
    /// Generic key/value store with TTL. CapNet uses this for nonce replay-protection, redeem
    /// token issuance, and (later) captcha sessions, all under internal key prefixes.
    /// Consumers wire it to whatever cache they already run — local memory in dev, Redis or
    /// Azure Cache for Redis in production. No atomic primitives required.
    /// </summary>
    public interface ICapStateStore
    {
        Task PutAsync(string key, string value, TimeSpan ttl);
        Task<string> GetAsync(string key);
        Task RemoveAsync(string key);
    }
}
