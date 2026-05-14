using System.Net;

namespace CapNet.Challenges
{
    /// <summary>
    /// Result of a redeem call. Owns the verbatim JSON capjs-core produced so the controller can
    /// hand it back to the widget without re-serialising.
    /// </summary>
    public sealed class RedeemOutcome
    {
        public bool Success { get; set; }
        public HttpStatusCode HttpStatus { get; set; }
        public string ResponseJson { get; set; }
        public string RedeemToken { get; set; }
        public long? Expires { get; set; }
        public string Reason { get; set; }
    }
}
