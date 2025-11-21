using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PROG6212_ST10449143_POE_PART_1.Models;
using System.ComponentModel.DataAnnotations;

namespace PROG6212_ST10449143_POE_PART_1.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;

        public AccountController(SignInManager<User> signInManager, UserManager<User> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                Console.WriteLine($"=== LOGIN ATTEMPT ===");
                Console.WriteLine($"Email: {model.Email}");
                Console.WriteLine($"Password Length: {model.Password?.Length}");

                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

                Console.WriteLine($"Login Result: {result.Succeeded}");

                if (result.Succeeded)
                {
                    Console.WriteLine($"Login successful for: {model.Email}");

                    var user = await _userManager.FindByEmailAsync(model.Email);
                    Console.WriteLine($"User found: {user != null}");

                    if (user != null)
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        Console.WriteLine($"User roles: {string.Join(", ", roles)}");

                        // Initiliaze seesion based on role
                        if (await _userManager.IsInRoleAsync(user, "Coordinator"))
                        {
                            HttpContext.Session.SetString($"CoordinatorAccess_{user.UserName}", DateTime.Now.ToString());
                            return RedirectToAction("CoordinatorApprovals", "Claims");
                        }
                        else if (await _userManager.IsInRoleAsync(user, "AcademicManager"))
                        {
                            HttpContext.Session.SetString($"ManagerAccess_{user.UserName}", DateTime.Now.ToString());
                            return RedirectToAction("ManagerApprovals", "Claims");
                        }
                        else if (await _userManager.IsInRoleAsync(user, "HR"))
                        {
                            return RedirectToAction("Dashboard", "HR");
                        }
                        else if (await _userManager.IsInRoleAsync(user, "Lecturer"))
                        {
                            return RedirectToAction("Submit", "Claims");
                        }
                    }

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    Console.WriteLine($"Login failed for: {model.Email}");
                    ModelState.AddModelError(string.Empty, "Invalid login attempt. Please check your credentials.");
                    return View(model);
                }
            }

            Console.WriteLine($"Model state invalid: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }

    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
}