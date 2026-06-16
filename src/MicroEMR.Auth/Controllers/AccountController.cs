using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Auth.Data;
using MicroEMR.Auth.Models;

namespace MicroEMR.Auth.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(
        SignInManager<ApplicationUser> signInManager)
    {
        _signInManager = signInManager;
    }


    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl
        });
    }


    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);


        var result =
            await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                true,
                false);


        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                "",
                "Invalid username or password");

            return View(model);
        }


        if (!string.IsNullOrEmpty(model.ReturnUrl))
            return Redirect(model.ReturnUrl);


        return Redirect("/");
    }
}