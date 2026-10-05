using Microsoft.AspNetCore.Mvc;

namespace SocialVideoDownloader.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
