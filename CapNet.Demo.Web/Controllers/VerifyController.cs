using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json;

namespace CapNet.Demo.Web.Controllers
{
    public class VerifyRequest
    {
        [JsonProperty("token")]
        public string Token { get; set; }
    }

    /// <summary>
    /// Server-side endpoint a relying party calls to consume a redeem token. Mirrors the role of
    /// Cap's <c>/siteverify</c>, but here it runs in-process so there's no second HTTP hop.
    /// </summary>
    [RoutePrefix("verify")]
    public class VerifyController : ApiController
    {
        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Verify([FromBody] VerifyRequest body)
        {
            if (body == null || string.IsNullOrEmpty(body.Token))
                return BadRequest("Missing token.");

            bool ok = await Startup.Service.VerifyRedeemTokenAsync(body.Token);
            return Json(new { success = ok });
        }
    }
}
