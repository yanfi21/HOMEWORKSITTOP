using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shop.Web.Models;
using Shop.Web.ViewModels;

namespace Shop.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly ILogger<AccountController> _logger;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        ILogger<AccountController> logger)
    {
        _users = users;
        _signIn = signIn;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName.Trim()
        };

        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _users.AddToRoleAsync(user, "Customer");
        await _signIn.SignInAsync(user, isPersistent: false);
        _logger.LogInformation("Зарегистрирован новый пользователь {Email}", model.Email);
        TempData["Toast"] = "Добро пожаловать в ТехноМаркет!";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")] // защита от подбора пароля: 10 попыток/мин + блокировка Identity
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.FindByEmailAsync(model.Email);
        if (user is null)
        {
            _logger.LogWarning("Попытка входа с несуществующим e-mail {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "Неверный e-mail или пароль");
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Пользователь {Email} временно заблокирован из-за неверных паролей", model.Email);
            ModelState.AddModelError(string.Empty, "Аккаунт временно заблокирован. Попробуйте позже.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Неудачный вход пользователя {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "Неверный e-mail или пароль");
            return View(model);
        }

        _logger.LogInformation("Пользователь {Email} вошёл в систему", model.Email);

        // Защита от Open Redirect: возвращаем только на локальные адреса.
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new ProfileViewModel
        {
            Email = user.Email ?? string.Empty,
            FullName = user.FullName ?? string.Empty,
            Phone = user.PhoneNumber,
            Address = user.Address
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        model.Email = user.Email ?? string.Empty;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = model.Phone?.Trim();
        user.Address = model.Address?.Trim();
        await _users.UpdateAsync(user);

        TempData["Toast"] = "Профиль сохранён";
        return RedirectToAction(nameof(Profile));
    }

    public IActionResult AccessDenied() => View();
}
