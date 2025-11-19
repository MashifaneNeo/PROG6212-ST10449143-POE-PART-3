using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PROG6212_ST10449143_POE_PART_1.Models;
using Microsoft.AspNetCore.Authorization;

namespace PROG6212_ST10449143_POE_PART_1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("HR"))
                    return RedirectToAction("Dashboard", "HR");
                else if (User.IsInRole("Lecturer"))
                    return RedirectToAction("Submit", "Claims");
                else if (User.IsInRole("Coordinator"))
                    return RedirectToAction("Approvals", "Claims");
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
