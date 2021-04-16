using Microsoft.AspNetCore.Mvc;

namespace Bodiless.Tests.Controllers
{
    [ApiController]
    [Route("echo")]
    public class EchoController : ControllerBase
    {
        [HttpGet("{text}")]
        public ActionResult<string> Echo(string text)
        {
            return Ok(text);
        }
    }
}
