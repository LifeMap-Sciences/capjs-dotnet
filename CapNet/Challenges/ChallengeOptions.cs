using System;
using Newtonsoft.Json;

namespace CapNet.Challenges
{
    /// <summary>
    /// Options forwarded to capjs-core's <c>generateChallenge</c>. Names match the JS API exactly
    /// so the bridge can pass them through without translation.
    /// </summary>
    public sealed class ChallengeOptions
    {
        [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
        public int? Format { get; set; }

        [JsonProperty("challengeCount", NullValueHandling = NullValueHandling.Ignore)]
        public int? ChallengeCount { get; set; }

        [JsonProperty("challengeSize", NullValueHandling = NullValueHandling.Ignore)]
        public int? ChallengeSize { get; set; }

        [JsonProperty("challengeDifficulty", NullValueHandling = NullValueHandling.Ignore)]
        public int? ChallengeDifficulty { get; set; }

        [JsonProperty("expiresMs", NullValueHandling = NullValueHandling.Ignore)]
        public long? ExpiresMs { get; set; }

        [JsonProperty("scope", NullValueHandling = NullValueHandling.Ignore)]
        public string Scope { get; set; }

        [JsonProperty("instrumentation", NullValueHandling = NullValueHandling.Ignore)]
        public object Instrumentation { get; set; }

        [JsonProperty("protocols", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Protocols { get; set; }

        [JsonProperty("keypair", NullValueHandling = NullValueHandling.Ignore)]
        public object Keypair { get; set; }

        [JsonProperty("t", NullValueHandling = NullValueHandling.Ignore)]
        public int? T { get; set; }

        [JsonIgnore]
        public TimeSpan RedeemTokenTtl { get; set; } = TimeSpan.FromMinutes(20);
    }
}
