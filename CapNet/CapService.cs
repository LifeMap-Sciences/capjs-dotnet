using System;
using System.Net;
using System.Threading.Tasks;
using CapNet.Challenges;
using CapNet.Storage;
using Jering.Javascript.NodeJS;
using Newtonsoft.Json;

namespace CapNet
{
    /// <summary>
    /// .NET facade over capjs-core, invoked through Jering. The library passes JSON strings to
    /// and from the bridge — no .NET-side parse/serialize round-trip — and only inspects the
    /// handful of fields it actually needs (the body's <c>token</c> for replay protection, and
    /// the outcome's <c>success</c>/<c>token</c>/<c>expires</c>/<c>reason</c>). All state lives
    /// in a single <see cref="ICapStateStore"/> the consumer wires to their existing cache.
    /// </summary>
    public sealed class CapService
    {
        private const string KeyPrefixNonce = "capnet:nonce:";
        private const string KeyPrefixRedeem = "capnet:redeem:";

        private readonly string _secret;
        private readonly INodeJSService _node;
        private readonly string _bridgePath;
        private readonly ICapStateStore _state;
        private readonly ChallengeOptions _defaults;
        private readonly string _defaultOptsJson;

        public CapService(
            string secret,
            INodeJSService node,
            string bridgePath,
            ICapStateStore state,
            ChallengeOptions defaults = null)
        {
            if (string.IsNullOrEmpty(secret) || System.Text.Encoding.UTF8.GetByteCount(secret) < 16)
                throw new ArgumentException("Secret must be at least 16 UTF-8 bytes.", nameof(secret));
            _secret = secret;
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _bridgePath = bridgePath ?? throw new ArgumentNullException(nameof(bridgePath));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _defaults = defaults ?? new ChallengeOptions();
            _defaultOptsJson = JsonConvert.SerializeObject(_defaults);
        }

        /// <summary>Returns capjs-core's challenge JSON verbatim — the widget consumes it directly.</summary>
        public Task<string> IssueChallengeJsonAsync(ChallengeOptions overrides = null)
        {
            string optsJson = overrides == null ? _defaultOptsJson : JsonConvert.SerializeObject(overrides);
            return _node.InvokeFromFileAsync<string>(
                _bridgePath, "generateChallenge",
                new object[] { _secret, optsJson });
        }

        public async Task<RedeemOutcome> RedeemAsync(string bodyJson, string scope = null)
        {
            if (string.IsNullOrEmpty(bodyJson))
                return Failure("invalid_body", HttpStatusCode.BadRequest);

            string optsJson = BuildValidateOptsJson(scope ?? _defaults.Scope);

            string responseJson;
            try
            {
                responseJson = await _node.InvokeFromFileAsync<string>(
                    _bridgePath, "validateChallenge",
                    new object[] { _secret, bodyJson, optsJson }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new RedeemOutcome
                {
                    Success = false,
                    HttpStatus = HttpStatusCode.InternalServerError,
                    Reason = "bridge_error",
                    ResponseJson = JsonConvert.SerializeObject(new { success = false, reason = "bridge_error", error = ex.Message }),
                };
            }

            ValidateOutcomeDto outcome = JsonConvert.DeserializeObject<ValidateOutcomeDto>(responseJson);
            if (outcome == null || !outcome.Success)
            {
                return new RedeemOutcome
                {
                    Success = false,
                    HttpStatus = HttpStatusCode.Forbidden,
                    Reason = outcome?.Reason,
                    ResponseJson = responseJson,
                };
            }

            // Fail-closed: a "success" outcome with no usable redeem token or expiry is a
            // capjs-core protocol violation we don't propagate.
            if (string.IsNullOrEmpty(outcome.Token) || !outcome.Expires.HasValue)
            {
                return Failure("invalid_outcome", HttpStatusCode.InternalServerError);
            }

            // Replay protection: atomically claim the JWT signature exactly once.
            string token = TryExtractBodyToken(bodyJson);
            if (string.IsNullOrEmpty(token))
                return Failure("invalid_body", HttpStatusCode.BadRequest);

            string nonceKey = KeyPrefixNonce + ExtractSignaturePart(token);
            bool claimed = await _state.TryPutIfAbsentAsync(nonceKey, "1", _defaults.RedeemTokenTtl).ConfigureAwait(false);
            if (!claimed)
            {
                return new RedeemOutcome
                {
                    Success = false,
                    HttpStatus = HttpStatusCode.Forbidden,
                    Reason = "already_redeemed",
                    ResponseJson = JsonConvert.SerializeObject(new { success = false, reason = "already_redeemed" }),
                };
            }

            // capjs-core has already minted the redeem token; store it for back-end /siteverify.
            long expiresMs = outcome.Expires.Value;
            TimeSpan ttl = DateTimeOffset.FromUnixTimeMilliseconds(expiresMs) - DateTimeOffset.UtcNow;
            if (ttl <= TimeSpan.Zero) ttl = TimeSpan.FromMinutes(1);
            await _state.PutAsync(KeyPrefixRedeem + outcome.Token, expiresMs.ToString(), ttl).ConfigureAwait(false);

            return new RedeemOutcome
            {
                Success = true,
                HttpStatus = HttpStatusCode.OK,
                RedeemToken = outcome.Token,
                Expires = expiresMs,
                ResponseJson = responseJson,
            };
        }

        /// <summary>Generate a fresh RSW keypair (~700 ms at 2048 bits). Persist and reuse.</summary>
        public Task<string> GenerateRswKeypairJsonAsync(int bits = 2048)
        {
            return _node.InvokeFromFileAsync<string>(
                _bridgePath, "generateRswKeypair", new object[] { bits });
        }

        public async Task<bool> VerifyRedeemTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            // Atomic read-and-delete — two concurrent verifications can never both see a non-null
            // stored value for the same token.
            string stored = await _state.GetAndRemoveAsync(KeyPrefixRedeem + token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(stored)) return false;
            if (!long.TryParse(stored, out long expiresMs)) return false;
            return expiresMs > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static RedeemOutcome Failure(string reason, HttpStatusCode status) => new RedeemOutcome
        {
            Success = false,
            HttpStatus = status,
            Reason = reason,
            ResponseJson = JsonConvert.SerializeObject(new { success = false, reason = reason }),
        };

        private static string BuildValidateOptsJson(string scope)
        {
            if (string.IsNullOrEmpty(scope)) return "{}";
            return JsonConvert.SerializeObject(new { scope });
        }

        private static string TryExtractBodyToken(string bodyJson)
        {
            try
            {
                var dto = JsonConvert.DeserializeAnonymousType(bodyJson, new { token = (string)null });
                return dto?.token;
            }
            catch { return null; }
        }

        private static string ExtractSignaturePart(string token)
        {
            int last = token.LastIndexOf('.');
            return last < 0 ? token : token.Substring(last + 1);
        }

        private sealed class ValidateOutcomeDto
        {
            [JsonProperty("success")] public bool Success { get; set; }
            [JsonProperty("token")] public string Token { get; set; }
            [JsonProperty("expires")] public long? Expires { get; set; }
            [JsonProperty("reason")] public string Reason { get; set; }
        }
    }
}
