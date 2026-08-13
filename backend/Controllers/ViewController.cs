using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [AllowAnonymous]
    public class ViewController : ControllerBase
    {
        [HttpGet("privacy")]
        public async Task<IActionResult> Privacy()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "templates", "privacy.html");
            if (!System.IO.File.Exists(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "..", "templates", "privacy.html");
            }

            if (System.IO.File.Exists(path))
            {
                var html = await System.IO.File.ReadAllTextAsync(path);
                return Content(html, "text/html");
            }

            return Content("<h1>Privacy Policy</h1><p>The privacy policy document is not available at this moment.</p>", "text/html");
        }

        [HttpGet("terms")]
        public async Task<IActionResult> Terms()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "templates", "terms.html");
            if (!System.IO.File.Exists(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "..", "templates", "terms.html");
            }

            if (System.IO.File.Exists(path))
            {
                var html = await System.IO.File.ReadAllTextAsync(path);
                return Content(html, "text/html");
            }

            return Content("<h1>Terms of Service</h1><p>The terms of service document is not available at this moment.</p>", "text/html");
        }
    }
}
