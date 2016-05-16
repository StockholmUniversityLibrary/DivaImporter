using System.Web.Mvc;

namespace Publiceringsverktyg.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return RedirectToAction("Index", "WoSImporting");

            //ViewBag.Title = "Hem";
            //ViewBag.Message = "Välkommen";
            //return View();
        }
       
    }
}
