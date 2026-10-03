using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.Models;
using PGManagement.ViewModels;

namespace PGManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<object> _passwordHasher;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<object>();
        }

        // =====================================================
        // REGISTER - USER / OWNER
        // =====================================================

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.UserType != "USER" &&
                model.UserType != "OWNER")
            {
                ModelState.AddModelError(
                    "UserType",
                    "Please select a valid registration type.");

                return View(model);
            }

            // =================================================
            // USER REGISTRATION
            // =================================================

            if (model.UserType == "USER")
            {
                var emailExists = await _context.Users
                    .AnyAsync(x => x.Email == model.Email);

                if (emailExists)
                {
                    ModelState.AddModelError(
                        "Email",
                        "Email already exists.");

                    return View(model);
                }

                var user = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Address = model.Address,
                    CreatedDate = DateTime.Now,
                    Status = "Active",

                    // Profile image initially empty
                    ProfileImagePath = null,

                    // Aadhaar initially empty
                    AadhaarNumber = null,
                    AadhaarImagePath = null
                };

                user.PasswordHash =
                    _passwordHasher.HashPassword(
                        user,
                        model.Password);

                _context.Users.Add(user);
            }

            // =================================================
            // OWNER REGISTRATION
            // =================================================

            else
            {
                var emailExists = await _context.Owners
                    .AnyAsync(x => x.Email == model.Email);

                if (emailExists)
                {
                    ModelState.AddModelError(
                        "Email",
                        "Email already exists.");

                    return View(model);
                }

                var owner = new Owner
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Address = model.Address,
                    CreatedDate = DateTime.Now,
                    Status = "Active"
                };

                owner.PasswordHash =
                    _passwordHasher.HashPassword(
                        owner,
                        model.Password);

                _context.Owners.Add(owner);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Registration successful. Please login.";

            return RedirectToAction("Login");
        }


        // =====================================================
        // LOGIN GET
        // =====================================================

        [HttpGet]
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }


        // =====================================================
        // LOGIN POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }


            // =================================================
            // USER LOGIN
            // =================================================

            if (model.UserType == "USER")
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(
                        x => x.Email == model.Email);

                if (user != null)
                {
                    var result =
                        _passwordHasher.VerifyHashedPassword(
                            user,
                            user.PasswordHash,
                            model.Password);

                    if (result ==
                        PasswordVerificationResult.Success)
                    {
                        // -----------------------------------------
                        // USER SESSION
                        // -----------------------------------------

                        HttpContext.Session.SetInt32(
                            "UserId",
                            user.UserId);

                        HttpContext.Session.SetString(
                            "UserType",
                            "USER");

                        // IMPORTANT:
                        // Store user's name
                        HttpContext.Session.SetString(
                            "UserName",
                            user.FullName ?? "User");

                        // IMPORTANT:
                        // Store user's profile image
                        HttpContext.Session.SetString(
                            "UserImage",
                            user.ProfileImagePath ?? "");

                        return RedirectToAction(
                            "Dashboard",
                            "User");
                    }
                }
            }


            // =================================================
            // OWNER LOGIN
            // =================================================

            else if (model.UserType == "OWNER")
            {
                var owner = await _context.Owners
                    .FirstOrDefaultAsync(
                        x => x.Email == model.Email);

                if (owner != null)
                {
                    var result =
                        _passwordHasher.VerifyHashedPassword(
                            owner,
                            owner.PasswordHash,
                            model.Password);

                    if (result ==
                        PasswordVerificationResult.Success)
                    {
                        // -----------------------------------------
                        // OWNER SESSION
                        // -----------------------------------------

                        HttpContext.Session.SetInt32(
                            "OwnerId",
                            owner.OwnerId);

                        HttpContext.Session.SetString(
                            "UserType",
                            "OWNER");

                        HttpContext.Session.SetString(
                            "UserName",
                            owner.FullName ?? "Owner");

                        return RedirectToAction(
                            "Dashboard",
                            "Owner");
                    }
                }
            }


            // =================================================
            // ADMIN LOGIN
            // =================================================

            else if (model.UserType == "ADMIN")
            {
                var admin = await _context.Admins
                    .FirstOrDefaultAsync(
                        x => x.Email == model.Email);

                if (admin != null)
                {
                    var result =
                        _passwordHasher.VerifyHashedPassword(
                            admin,
                            admin.PasswordHash,
                            model.Password);

                    if (result ==
                        PasswordVerificationResult.Success)
                    {
                        // -----------------------------------------
                        // ADMIN SESSION
                        // -----------------------------------------

                        HttpContext.Session.SetInt32(
                            "AdminId",
                            admin.AdminId);

                        HttpContext.Session.SetString(
                            "UserType",
                            "ADMIN");

                        HttpContext.Session.SetString(
                            "UserName",
                            admin.FullName ?? "Admin");

                        return RedirectToAction(
                            "Dashboard",
                            "Admin");
                    }
                }
            }


            // =================================================
            // INVALID LOGIN
            // =================================================

            ModelState.AddModelError(
                "",
                "Invalid email or password.");

            return View(model);
        }


        // =====================================================
        // ADMIN REGISTER GET
        // =====================================================

        [HttpGet]
        public IActionResult AdminRegister()
        {
            var model = new RegisterViewModel
            {
                UserType = "ADMIN"
            };

            return View(model);
        }


        // =====================================================
        // ADMIN REGISTER POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminRegister(
            RegisterViewModel model)
        {
            model.UserType = "ADMIN";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var emailExists = await _context.Admins
                .AnyAsync(x => x.Email == model.Email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    "Email",
                    "Admin email already exists.");

                return View(model);
            }

            var admin = new Admin
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                Address = model.Address,
                CreatedDate = DateTime.Now,
                Status = "Active"
            };

            admin.PasswordHash =
                _passwordHasher.HashPassword(
                    admin,
                    model.Password);

            _context.Admins.Add(admin);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Admin registration successful. Please login.";

            return RedirectToAction("Login");
        }


        // =====================================================
        // LOGOUT
        // =====================================================

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return RedirectToAction(
                "Login",
                "Account");
        }
    }
}