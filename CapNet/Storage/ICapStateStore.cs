using System;
using System.Threading.Tasks;

namespace CapNet.Storage
{
    /// <summary>
    /// Generic key/value store with TTL. CapNet uses this for nonce replay-protection, redeem
    /// token issuance, and (later) captcha sessions, all under internal key prefixes.
    /// Consumers wire it to whatever cache they already run — local memory in dev, Redis or
    /// Azure Cache for Redis in production.
    ///
    /// Two of these operations need to be atomic for the replay guarantees to hold under
    /// concurrent requests: <see cref="TryPutIfAbsentAsync"/> (the nonce claim, must be a
    /// compare-and-set) and <see cref="GetAndRemoveAsync"/> (the redeem-token consume,
    /// must be an atomic read-and-delete). Redis maps these to <c>SET key value NX EX ttl</c>
    /// and <c>GETDEL key</c> respectively. In-memory stores can use a lock.
    /// </summary>
    public interface ICapStateStore
    {
        Task PutAsync(string key, string value, TimeSpan ttl);
        Task<string> GetAsync(string key);
        Task RemoveAsync(string key);

        /// <summary>
        /// Atomic claim: store <paramref name="value"/> under <paramref name="key"/> with the
        /// given TTL only if no value is present. Returns true if this caller won the claim,
        /// false if the key was already set.
        /// </summary>
        Task<bool> TryPutIfAbsentAsync(string key, string value, TimeSpan ttl);

        /// <summary>
        /// Atomic read-and-remove. Returns the prior value (or null if no key was present)
        /// and ensures the key is gone afterward. No two callers should ever see the same
        /// non-null return for the same key.
        /// </summary>
        Task<string> GetAndRemoveAsync(string key);
    }
}
