using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.Models;

namespace PGManagement.Controllers
{
    public class FoodMenuController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FoodMenuController(ApplicationDbContext context)
        {
            _context = context;
        }


        // =========================================================
        // USER - VIEW FOOD MENU
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Index(int? pgId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }

            var query = _context.FoodMenus
                .Include(x => x.PG)
                .Where(x => x.IsAvailable);

            if (pgId.HasValue)
            {
                query = query.Where(x => x.PGId == pgId.Value);
            }

            // First execute database query
            var menus = await query.ToListAsync();

            // Then perform custom sorting in C#
            menus = menus
                .OrderBy(x => x.PGId)
                .ThenBy(x => GetDayOrder(x.DayOfWeek))
                .ThenBy(x => GetMealOrder(x.MealType))
                .ThenBy(x => x.ItemName)
                .ToList();

            ViewBag.SelectedPG = pgId;

            return View(menus);
        }


        // =========================================================
        // OWNER - MANAGE FOOD MENU
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Manage(int? pgId)
        {
            var ownerId = HttpContext.Session.GetInt32("OwnerId");
            var userType = HttpContext.Session.GetString("UserType");

            if (ownerId == null || userType != "OWNER")
            {
                return RedirectToAction("Login", "Account");
            }


            // Get owner's PGs
            var pgs = await _context.PGs
                .Where(x => x.OwnerId == ownerId.Value)
                .OrderBy(x => x.PGName)
                .ToListAsync();

            ViewBag.PGs = pgs;
            ViewBag.SelectedPG = pgId;


            // Get food menus
            var query = _context.FoodMenus
                .Include(x => x.PG)
                .Where(x =>
                    x.PG != null &&
                    x.PG.OwnerId == ownerId.Value);


            // Filter selected PG
            if (pgId.HasValue)
            {
                query = query.Where(x => x.PGId == pgId.Value);
            }


            // IMPORTANT:
            // Execute SQL first
            var menus = await query.ToListAsync();


            // Custom day + meal sorting happens in C#
            menus = menus
                .OrderBy(x => x.PGId)
                .ThenBy(x => GetDayOrder(x.DayOfWeek))
                .ThenBy(x => GetMealOrder(x.MealType))
                .ThenBy(x => x.ItemName)
                .ToList();


            return View(menus);
        }


        // =========================================================
        // CREATE - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var ownerId = HttpContext.Session.GetInt32("OwnerId");
            var userType = HttpContext.Session.GetString("UserType");

            if (ownerId == null || userType != "OWNER")
            {
                return RedirectToAction("Login", "Account");
            }


            var pgs = await _context.PGs
                .Where(x => x.OwnerId == ownerId.Value)
                .OrderBy(x => x.PGName)
                .ToListAsync();

            ViewBag.PGs = pgs;

            return View(new FoodMenu
            {
                IsAvailable = true,
                DayOfWeek = "Monday"
            });
        }


        // =========================================================
        // CREATE - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FoodMenu model)
        {
            var ownerId = HttpContext.Session.GetInt32("OwnerId");
            var userType = HttpContext.Session.GetString("UserType");

            if (ownerId == null || userType != "OWNER")
            {
                return RedirectToAction("Login", "Account");
            }


            // Clean values
            model.ItemName = model.ItemName?.Trim() ?? string.Empty;
            model.MealType = model.MealType?.Trim() ?? string.Empty;
            model.DayOfWeek = model.DayOfWeek?.Trim() ?? string.Empty;
            model.Description = model.Description?.Trim();


            // Validate PG ownership
            var pg = await _context.PGs
                .FirstOrDefaultAsync(x =>
                    x.PGId == model.PGId &&
                    x.OwnerId == ownerId.Value);

            if (pg == null)
            {
                ModelState.AddModelError("PGId", "Please select a valid PG.");
            }


            // Validate day
            if (!IsValidDay(model.DayOfWeek))
            {
                ModelState.AddModelError(
                    "DayOfWeek",
                    "Please select a valid day.");
            }


            // Validate meal
            if (!IsValidMeal(model.MealType))
            {
                ModelState.AddModelError(
                    "MealType",
                    "Please select a valid meal type.");
            }


            if (!ModelState.IsValid)
            {
                var pgs = await _context.PGs
                    .Where(x => x.OwnerId == ownerId.Value)
                    .OrderBy(x => x.PGName)
                    .ToListAsync();

                ViewBag.PGs = pgs;

                return View(model);
            }


            model.CreatedDate = DateTime.Now;

            _context.FoodMenus.Add(model);

            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Food menu item added successfully.";


            return RedirectToAction(
                nameof(Manage),
                new { pgId = model.PGId });
        }


        // =========================================================
        // EDIT - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var ownerId = HttpContext.Session.GetInt32("OwnerId");
            var userType = HttpContext.Session.GetString("UserType");

            if (ownerId == null || userType != "OWNER")
            {
                return RedirectToAction("Login", "Account");
            }


            var menu = await _context.FoodMenus
                .Include(x => x.PG)
                .FirstOrDefaultAsync(x =>
                    x.FoodMenuId == id &&
                    x.PG != null &&
                    x.PG.OwnerId == ownerId.Value);


            if (menu == null)
            {
                return NotFound();
            }


            var pgs = await _context.PGs
                .Where(x => x.OwnerId == ownerId.Value)
                .OrderBy(x => x.PGName)
                .ToListAsync();

            ViewBag.PGs = pgs;


            return View(menu);
        }


        // =========================================================
        // EDIT - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            FoodMenu model)
        {
            var ownerId = HttpContext.Session.GetInt32("OwnerId");
            var userType = HttpContext.Session.GetString("UserType");

            if (ownerId == null || userType != "OWNER")
            {
                return RedirectToAction("Login", "Account");
            }


            if (id != model.FoodMenuId)
            {
                return BadRequest();
            }


            // Clean values
            model.ItemName = model.ItemName?.Trim() ?? string.Empty;
            model.MealType = model.MealType?.Trim() ?? string.Empty;
            model.DayOfWeek = model.DayOfWeek?.Trim() ?? string.Empty;
            model.Description = model.Description?.Trim();


            // Check PG ownership
            var pg = await _context.PGs
                .FirstOrDefaultAsync(x =>
                    x.PGId == model.PGId &&
                    x.OwnerId == ownerId.Value);

            if (pg == null)
            {
                ModelState.AddModelError(
                    "PGId",
                    "Please select a valid PG.");
            }


            // Validate day
            if (!IsValidDay(model.DayOfWeek))
            {
                ModelState.AddModelError(
                    "DayOfWeek",
                    "Please select a valid day.");
            }


            // Validate meal
            if (!IsValidMeal(model.MealType))
            {
                ModelState.AddModelError(
                    "MealType",
                    "Please select a valid meal type.");
            }


            if (!ModelState.IsValid)
            {
                var pgs = await _context.PGs
                    .Where(x => x.OwnerId == ownerId.Value)
                    .OrderBy(x => x.PGName)
                    .ToListAsync();

                ViewBag.PGs = pgs;

                return View(model);
            }


            // Get existing menu
            var existingMenu = await _context.FoodMenus
                .Include(x => x.PG)
                .FirstOrDefaultAsync(x =>
                    x.FoodMenuId == id &&
                    x.PG != null &&
                    x.PG.OwnerId == ownerId.Value);


            if (existingMenu == null)
            {
                return NotFound();
            }


            // Update fields
            existingMenu.ItemName = model.ItemName;
            existingMenu.MealType = model.MealType;
            existingMenu.DayOfWeek = model.DayOfWeek;
            existingMenu.Description = model.Description;
            existingMenu.IsAvailable = model.IsAvailable;
            existingMenu.PGId = model.PGId;


            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Food menu updated successfully.";


            return RedirectToAction(
                nameof(Manage),
                new { pgId = model.PGId });
        }


        // =========================================================
        // DELETE
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ownerId = HttpContext.Session.GetInt32("OwnerId");
            var userType = HttpContext.Session.GetString("UserType");

            if (ownerId == null || userType != "OWNER")
            {
                return RedirectToAction("Login", "Account");
            }


            var menu = await _context.FoodMenus
                .Include(x => x.PG)
                .FirstOrDefaultAsync(x =>
                    x.FoodMenuId == id &&
                    x.PG != null &&
                    x.PG.OwnerId == ownerId.Value);


            if (menu == null)
            {
                return NotFound();
            }


            var pgId = menu.PGId;


            _context.FoodMenus.Remove(menu);

            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Food menu item deleted successfully.";


            return RedirectToAction(
                nameof(Manage),
                new { pgId = pgId });
        }


        // =========================================================
        // VALIDATE DAY
        // =========================================================

        private static bool IsValidDay(string? day)
        {
            return day switch
            {
                "Monday" => true,
                "Tuesday" => true,
                "Wednesday" => true,
                "Thursday" => true,
                "Friday" => true,
                "Saturday" => true,
                "Sunday" => true,
                _ => false
            };
        }


        // =========================================================
        // VALIDATE MEAL
        // =========================================================

        private static bool IsValidMeal(string? meal)
        {
            return meal switch
            {
                "Breakfast" => true,
                "Lunch" => true,
                "Dinner" => true,
                "Snacks" => true,
                _ => false
            };
        }


        // =========================================================
        // DAY ORDER
        // =========================================================

        private static int GetDayOrder(string? day)
        {
            return day switch
            {
                "Monday" => 1,
                "Tuesday" => 2,
                "Wednesday" => 3,
                "Thursday" => 4,
                "Friday" => 5,
                "Saturday" => 6,
                "Sunday" => 7,
                _ => 99
            };
        }


        // =========================================================
        // MEAL ORDER
        // =========================================================

        private static int GetMealOrder(string? meal)
        {
            return meal switch
            {
                "Breakfast" => 1,
                "Lunch" => 2,
                "Snacks" => 3,
                "Dinner" => 4,
                _ => 99
            };
        }
    }
}