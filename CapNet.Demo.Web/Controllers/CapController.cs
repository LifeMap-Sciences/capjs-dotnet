using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using CapNet.Challenges;

namespace CapNet.Demo.Web.Controllers
{
    public class CapController : ApiController
    {
        // ── Armed (production-shaped) endpoints ───────────────────────────────
        [HttpPost, Route("cap/challenge")]
        public async Task<HttpResponseMessage> Challenge()
        {
            string json = await Startup.Service.IssueChallengeJsonAsync(Startup.ArmedOptions);
            return JsonResponse(System.Net.HttpStatusCode.OK, json);
        }

        [HttpPost, Route("cap/redeem")]
        public async Task<HttpResponseMessage> Redeem(HttpRequestMessage request)
        {
            return await DoRedeem(request).ConfigureAwait(false);
        }

        // ── Test endpoints (instrumentation present but non-blocking) ─────────
        [HttpPost, Route("cap-test/challenge")]
        public async Task<HttpResponseMessage> ChallengeTest()
        {
            string json = await Startup.Service.IssueChallengeJsonAsync(Startup.TestOptions);
            return JsonResponse(System.Net.HttpStatusCode.OK, json);
        }

        [HttpPost, Route("cap-test/redeem")]
        public async Task<HttpResponseMessage> RedeemTest(HttpRequestMessage request)
        {
            return await DoRedeem(request).ConfigureAwait(false);
        }

        // ── Format 2 endpoints (PoW + RSW + instrumentation) ──────────────────
        [HttpPost, Route("cap-v2/challenge")]
        public async Task<HttpResponseMessage> ChallengeV2()
        {
            string json = await Startup.Service.IssueChallengeJsonAsync(Startup.Format2Options);
            return JsonResponse(System.Net.HttpStatusCode.OK, json);
        }

        [HttpPost, Route("cap-v2/redeem")]
        public async Task<HttpResponseMessage> RedeemV2(HttpRequestMessage request)
        {
            return await DoRedeem(request).ConfigureAwait(false);
        }

        private static async Task<HttpResponseMessage> DoRedeem(HttpRequestMessage request)
        {
            string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
            RedeemOutcome outcome = await Startup.Service.RedeemAsync(body).ConfigureAwait(false);
            return JsonResponse(outcome.HttpStatus, outcome.ResponseJson);
        }

        private static HttpResponseMessage JsonResponse(System.Net.HttpStatusCode status, string json)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json ?? "{}", Encoding.UTF8, "application/json"),
            };
        }
    }
}
