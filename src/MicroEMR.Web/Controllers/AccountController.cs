using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroEMR.Web.Controllers;

[Authorize]
public class AccountController : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        return SignOut(
            new AuthenticationProperties
            {
                RedirectUri = "/Account/Login"
            },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme
        );
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return Challenge(
            new AuthenticationProperties
            {
                RedirectUri = Url.IsLocalUrl(returnUrl)
                    ? returnUrl
                    : Url.Action("Index", "Home")
            },
            OpenIdConnectDefaults.AuthenticationScheme
        );
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}