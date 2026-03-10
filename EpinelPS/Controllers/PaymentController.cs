using EpinelPS.Database;
using EpinelPS.Utils;
using Microsoft.AspNetCore.Mvc;

namespace EpinelPS.Controllers
{
    [Route("payment")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        [HttpGet("jupiter/success")]
        public IActionResult JupiterSuccess([FromQuery] string referenceId)
        {
            if (string.IsNullOrWhiteSpace(referenceId))
            {
                return BadRequest("missing referenceId");
            }

            if (JsonDb.Instance.SimulatedPurchaseOrders.TryGetValue(referenceId, out var order))
            {
                const string html = "<!doctype html><html><head><meta charset='utf-8'><title>Payment</title></head><body><script>try{window.close();}catch(e){}setTimeout(function(){window.location='about:blank';},50);</script>Payment OK</body></html>";
                return Content(html, "text/html");
            }

            Logging.WriteLine($"[JupiterSandbox] Unknown referenceId in success redirect: {referenceId}", LogType.Warning);
            return NotFound("unknown referenceId");
        }
    }
}
