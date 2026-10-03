using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.ViewModels;

namespace PGManagement.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // ADMIN DASHBOARD
        // ==========================================

        public async Task<IActionResult> Dashboard()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            var userType = HttpContext.Session.GetString("UserType");

            if (adminId == null || userType != "ADMIN")
            {
                return RedirectToAction("Login", "Account");
            }

            var dashboard = new AdminDashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),

                TotalOwners = await _context.Owners.CountAsync(),

                TotalAdmins = await _context.Admins.CountAsync(),

                ActiveUsers = await _context.Users
                    .CountAsync(x => x.Status == "Active"),

                ActiveOwners = await _context.Owners
                    .CountAsync(x => x.Status == "Active"),

                ActiveAdmins = await _context.Admins
                    .CountAsync(x => x.Status == "Active")
            };

            return View(dashboard);
        }


        // ==========================================
        // PENDING PGs
        // ==========================================

        public async Task<IActionResult> PendingPGs()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            var userType = HttpContext.Session.GetString("UserType");

            if (adminId == null || userType != "ADMIN")
            {
                return RedirectToAction("Login", "Account");
            }

            var pgs = await _context.PGs
                .Include(x => x.Owner)
                .Where(x => x.Status == "Pending")
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            return View(pgs);
        }


        // ==========================================
        // APPROVE PG
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePG(int id)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            var userType = HttpContext.Session.GetString("UserType");

            if (adminId == null || userType != "ADMIN")
            {
                return RedirectToAction("Login", "Account");
            }

            var pg = await _context.PGs
                .FirstOrDefaultAsync(x => x.PGId == id);

            if (pg == null)
            {
                return NotFound();
            }

            pg.Status = "Approved";

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "PG approved successfully.";

            return RedirectToAction(nameof(PendingPGs));
        }


        // ==========================================
        // REJECT PG
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPG(int id)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            var userType = HttpContext.Session.GetString("UserType");

            if (adminId == null || userType != "ADMIN")
            {
                return RedirectToAction("Login", "Account");
            }

            var pg = await _context.PGs
                .FirstOrDefaultAsync(x => x.PGId == id);

            if (pg == null)
            {
                return NotFound();
            }

            pg.Status = "Rejected";

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "PG rejected successfully.";

            return RedirectToAction(nameof(PendingPGs));
        }
    }
}