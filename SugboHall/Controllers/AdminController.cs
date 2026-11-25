using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HallBook.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HallBook.Controllers;

public class AdminController : Controller
{
    private readonly HallBookDb _db;
    private readonly IConfiguration _cfg;
    public AdminController(HallBookDb db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    [Authorize]
    public IActionResult Index() => RedirectToAction(nameof(Dashboard));

    [Authorize]
    public IActionResult Dashboard() => View(_db.GetDashboard());

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction(nameof(Dashboard));
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string? username, string? password, string? returnUrl)
    {
        var u = _cfg["Admin:Username"] ?? "admin";
        var p = _cfg["Admin:Password"] ?? "";

        bool ok = p.Length > 0 && Same(username ?? "", u) & Same(password ?? "", p);
        if (!ok)
        {
            ViewBag.Error = "Wrong username or password. Please try again.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, u), new Claim(ClaimTypes.Role, "Admin") },
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(Dashboard));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    // constant-time comparison
    private static bool Same(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)),
            SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
