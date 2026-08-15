using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [AllowAnonymous] // Authorization state is checked securely on the client-side via JWT in LocalStorage!
    [Route("Dashboard")]
    public class DashboardController : Controller
    {
        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("Tickets")]
        public IActionResult Tickets()
        {
            return View();
        }

        [HttpGet("Analytics")]
        public IActionResult Analytics()
        {
            return View();
        }

        [HttpGet("KnowledgeBase")]
        public IActionResult KnowledgeBase()
        {
            return View();
        }

        [HttpGet("CannedResponses")]
        public IActionResult CannedResponses()
        {
            return View();
        }

        [HttpGet("Settings")]
        public IActionResult Settings()
        {
            return View();
        }
    }
}
