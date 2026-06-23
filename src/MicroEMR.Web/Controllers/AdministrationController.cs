using MicroEMR.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroEMR.Web.Controllers;

[Authorize(Roles = AppRoles.Administrator)]
public class AdministrationController : Controller
{
    public IActionResult Users()
    {
        return View();
    }

    public IActionResult Resources()
    {
        return View();
    }

    public IActionResult Settings()
    {
        return View();
    }
}