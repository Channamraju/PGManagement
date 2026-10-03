using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;

namespace PGManagement.Controllers
{
    public class OwnerTenantController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OwnerTenantController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // OWNER AUTHENTICATION
        // =========================================================

        private bool IsOwner(out int ownerId)
        {
            var sessionOwnerId =
                HttpContext.Session.GetInt32("OwnerId");

            var userType =
                HttpContext.Session.GetString("UserType");

            if (sessionOwnerId == null || userType != "OWNER")
            {
                ownerId = 0;
                return false;
            }

            ownerId = sessionOwnerId.Value;
            return true;
        }


        // =========================================================
        // TENANT LIST
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var tenants = await _context.Bookings
                .AsNoTracking()

                .Include(b => b.User)
                .Include(b => b.PG)
                .Include(b => b.Room)
                .Include(b => b.Bed)
                .Include(b => b.Payments)

                .Where(b =>
                    b.PG != null &&
                    b.PG.OwnerId == ownerId)

                .OrderByDescending(b => b.BookingDate)

                .ToListAsync();

            return View(tenants);
        }


        // =========================================================
        // TENANT DETAILS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (id == null)
            {
                return NotFound();
            }

            var tenant = await _context.Bookings

                .Include(b => b.User)
                .Include(b => b.PG)
                .Include(b => b.Room)
                .Include(b => b.Bed)
                .Include(b => b.Payments)

                .FirstOrDefaultAsync(b =>
                    b.BookingId == id.Value &&
                    b.PG != null &&
                    b.PG.OwnerId == ownerId);

            if (tenant == null)
            {
                return NotFound();
            }

            return View(tenant);
        }


        // =========================================================
        // APPROVE BOOKING
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var booking = await _context.Bookings

                .Include(b => b.PG)
                .Include(b => b.Room)
                .Include(b => b.Bed)

                .FirstOrDefaultAsync(b =>
                    b.BookingId == id &&
                    b.PG != null &&
                    b.PG.OwnerId == ownerId);

            if (booking == null)
            {
                return NotFound();
            }


            // -----------------------------------------------------
            // ONLY PENDING BOOKINGS
            // -----------------------------------------------------

            if (booking.Status != "Pending")
            {
                TempData["Error"] =
                    "Only pending bookings can be approved.";

                return RedirectToAction(nameof(Index));
            }


            // -----------------------------------------------------
            // CHECK BED
            // -----------------------------------------------------

            if (booking.Bed == null)
            {
                TempData["Error"] =
                    "Selected bed was not found.";

                return RedirectToAction(nameof(Index));
            }


            // -----------------------------------------------------
            // CHECK ROOM
            // -----------------------------------------------------

            if (booking.Room == null)
            {
                TempData["Error"] =
                    "Selected room was not found.";

                return RedirectToAction(nameof(Index));
            }


            // -----------------------------------------------------
            // CHECK BED AVAILABILITY
            // -----------------------------------------------------

            if (booking.Bed.Status != "Available")
            {
                TempData["Error"] =
                    "This bed is already occupied or unavailable.";

                return RedirectToAction(nameof(Index));
            }


            // -----------------------------------------------------
            // CONFIRM BOOKING
            // -----------------------------------------------------

            booking.Status = "Confirmed";

            // Occupy bed
            booking.Bed.Status = "Occupied";


            // -----------------------------------------------------
            // CALCULATE AVAILABLE BEDS
            // -----------------------------------------------------

            var availableBeds = await _context.Beds
                .CountAsync(b =>
                    b.RoomId == booking.RoomId &&
                    b.Status == "Available");

            booking.Room.AvailableBeds = availableBeds;

            booking.Room.Status =
                availableBeds > 0
                    ? "Available"
                    : "Full";


            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Booking approved successfully.";

            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // REJECT BOOKING
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var booking = await _context.Bookings

                .Include(b => b.PG)
                .Include(b => b.Bed)

                .FirstOrDefaultAsync(b =>
                    b.BookingId == id &&
                    b.PG != null &&
                    b.PG.OwnerId == ownerId);

            if (booking == null)
            {
                return NotFound();
            }


            // -----------------------------------------------------
            // ONLY PENDING BOOKINGS
            // -----------------------------------------------------

            if (booking.Status != "Pending")
            {
                TempData["Error"] =
                    "Only pending bookings can be rejected.";

                return RedirectToAction(nameof(Index));
            }


            // -----------------------------------------------------
            // CANCEL BOOKING
            // -----------------------------------------------------

            booking.Status = "Cancelled";


            // Pending bookings normally don't occupy beds.
            // Keep the bed available if it still exists.

            if (booking.Bed != null)
            {
                booking.Bed.Status = "Available";
            }


            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Booking rejected successfully.";

            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // CHECKOUT TENANT
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(int id)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var booking = await _context.Bookings

                .Include(b => b.PG)
                .Include(b => b.Room)
                .Include(b => b.Bed)

                .FirstOrDefaultAsync(b =>
                    b.BookingId == id &&
                    b.PG != null &&
                    b.PG.OwnerId == ownerId);

            if (booking == null)
            {
                return NotFound();
            }


            // -----------------------------------------------------
            // ONLY CONFIRMED BOOKINGS
            // -----------------------------------------------------

            if (booking.Status != "Confirmed")
            {
                TempData["Error"] =
                    "Only confirmed tenants can be checked out.";

                return RedirectToAction(nameof(Index));
            }


            // -----------------------------------------------------
            // COMPLETE BOOKING
            // -----------------------------------------------------

            booking.Status = "Completed";


            // -----------------------------------------------------
            // FREE BED
            // -----------------------------------------------------

            if (booking.Bed != null)
            {
                booking.Bed.Status = "Available";
            }


            // -----------------------------------------------------
            // RECALCULATE ROOM AVAILABILITY
            // -----------------------------------------------------

            if (booking.Room != null)
            {
                var availableBeds = await _context.Beds
                    .CountAsync(b =>
                        b.RoomId == booking.RoomId &&
                        b.Status == "Available");

                booking.Room.AvailableBeds = availableBeds;

                booking.Room.Status =
                    availableBeds > 0
                        ? "Available"
                        : "Full";
            }


            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Tenant checked out successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}