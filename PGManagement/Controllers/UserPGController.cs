using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;

namespace PGManagement.Controllers
{
    public class UserPGController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserPGController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: UserPG/Index
        public async Task<IActionResult> Index(string? city)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }

            var query = _context.PGs
                .Include(x => x.Owner)
                .Where(x => x.Status == "Approved");

            if (!string.IsNullOrWhiteSpace(city))
            {
                string searchCity = city.Trim().ToLower();

                query = query.Where(x =>
                    x.City.ToLower().Contains(searchCity));
            }

            var pgs = await query
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            ViewBag.City = city;

            return View(pgs);
        }

        // GET: UserPG/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }

            var pg = await _context.PGs
                .Include(x => x.Owner)
                .Include(x => x.Rooms)
                    .ThenInclude(x => x.Beds)
                .FirstOrDefaultAsync(x =>
                    x.PGId == id &&
                    x.Status == "Approved");

            if (pg == null)
            {
                return NotFound();
            }

            return View(pg);
        }
    }
}