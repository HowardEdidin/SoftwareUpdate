#region

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Web.Models;

#endregion

namespace Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        //public IActionResult About()
        //{
        //    ViewData["Message"] = "Your application description page.";

        //    return View();
        //}
        public IActionResult GuestExe()
        {
            return View();
        }


        public IActionResult StartUpdate()
        {

            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }
    }
}