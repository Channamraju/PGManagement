using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.Models;

namespace PGManagement.Controllers
{
    public class OwnerBookingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OwnerBookingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // CHECK OWNER LOGIN
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
        // OWNER BOOKINGS
        // GET: OwnerBooking/Index
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.PG)
                .Include(x => x.Room)
                .Include(x => x.Bed)
                .Include(x => x.Payments)
                .Where(x =>
                    x.PG != null &&
                    x.PG.OwnerId == ownerId)
                .OrderByDescending(x => x.BookingDate)
                .ToListAsync();

            return View(bookings);
        }


        // =========================================================
        // BOOKING DETAILS
        // GET: OwnerBooking/Details/5
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var booking = await _context.Bookings
                .Include(x => x.User)
                .Include(x => x.PG)
                .Include(x => x.Room)
                .Include(x => x.Bed)
                .Include(x => x.Payments)
                .FirstOrDefaultAsync(x =>
                    x.BookingId == id &&
                    x.PG != null &&
                    x.PG.OwnerId == ownerId);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }


        // =========================================================
        // APPROVE BOOKING
        // POST: OwnerBooking/Approve/5
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
                .Include(x => x.PG)
                .Include(x => x.Room)
                .Include(x => x.Bed)
                .FirstOrDefaultAsync(x =>
                    x.BookingId == id &&
                    x.PG != null &&
                    x.PG.OwnerId == ownerId);

            if (booking == null)
            {
                return NotFound();
            }


            // -----------------------------------------------------
            // ONLY PENDING BOOKINGS CAN BE APPROVED
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
                    "The selected bed could not be found.";

                return RedirectToAction(nameof(Index));
            }


            // -----------------------------------------------------
            // CHECK BED AVAILABILITY
            // -----------------------------------------------------

            if (booking.Bed.Status != "Available")
            {
                TempData["Error"] =
                    "This bed is no longer available.";

                return RedirectToAction(nameof(Index));
            }


            // -----------------------------------------------------
            // CHECK ROOM
            // -----------------------------------------------------

            if (booking.Room == null)
            {
                TempData["Error"] =
                    "The selected room could not be found.";

                return RedirectToAction(nameof(Index));
            }


            // -----------------------------------------------------
            // CHECK ROOM AVAILABLE BEDS
            // -----------------------------------------------------

            if (booking.Room.AvailableBeds <= 0)
            {
                TempData["Error"] =
                    "This room has no available beds.";

                return RedirectToAction(nameof(Index));
            }


            // -----------------------------------------------------
            // CONFIRM BOOKING
            // -----------------------------------------------------

            booking.Status = "Confirmed";

            booking.Bed.Status = "Occupied";


            // -----------------------------------------------------
            // REDUCE AVAILABLE BEDS
            // -----------------------------------------------------

            booking.Room.AvailableBeds--;


            // -----------------------------------------------------
            // UPDATE ROOM STATUS
            // -----------------------------------------------------

            booking.Room.Status =
                booking.Room.AvailableBeds > 0
                    ? "Available"
                    : "Full";


            // -----------------------------------------------------
            // SAVE
            // -----------------------------------------------------

            await _context.SaveChangesAsync();


            TempData["Success"] =
                $"Booking #{booking.BookingId} approved successfully.";


            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // REJECT BOOKING
        // POST: OwnerBooking/Reject/5
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
                .Include(x => x.PG)
                .Include(x => x.Room)
                .Include(x => x.Bed)
                .FirstOrDefaultAsync(x =>
                    x.BookingId == id &&
                    x.PG != null &&
                    x.PG.OwnerId == ownerId);

            if (booking == null)
            {
                return NotFound();
            }


            // -----------------------------------------------------
            // ONLY PENDING BOOKINGS CAN BE REJECTED
            // -----------------------------------------------------

            if (booking.Status != "Pending")
            {
                TempData["Error"] =
                    "Only pending bookings can be rejected.";

                return RedirectToAction(nameof(Index));
            }


            // -----------------------------------------------------
            // REJECT BOOKING
            // -----------------------------------------------------

            booking.Status = "Cancelled";


            // -----------------------------------------------------
            // KEEP BED AVAILABLE
            // -----------------------------------------------------

            if (booking.Bed != null)
            {
                booking.Bed.Status = "Available";
            }


            // -----------------------------------------------------
            // SAVE
            // -----------------------------------------------------

            await _context.SaveChangesAsync();


            TempData["Success"] =
                $"Booking #{booking.BookingId} rejected successfully.";


            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // CHECKOUT / COMPLETE BOOKING
        // POST: OwnerBooking/Checkout/5
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
                .Include(x => x.PG)
                .Include(x => x.Room)
                .Include(x => x.Bed)
                .FirstOrDefaultAsync(x =>
                    x.BookingId == id &&
                    x.PG != null &&
                    x.PG.OwnerId == ownerId);

            if (booking == null)
            {
                return NotFound();
            }


            // -----------------------------------------------------
            // ONLY CONFIRMED BOOKINGS CAN BE COMPLETED
            // -----------------------------------------------------

            if (booking.Status != "Confirmed")
            {
                TempData["Error"] =
                    "Only confirmed bookings can be checked out.";

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
            // UPDATE ROOM AVAILABLE BEDS
            // -----------------------------------------------------

            if (booking.Room != null)
            {
                booking.Room.AvailableBeds++;

                if (booking.Room.AvailableBeds >
                    booking.Room.Capacity)
                {
                    booking.Room.AvailableBeds =
                        booking.Room.Capacity;
                }

                booking.Room.Status = "Available";
            }


            // -----------------------------------------------------
            // SAVE
            // -----------------------------------------------------

            await _context.SaveChangesAsync();


            TempData["Success"] =
                $"Booking #{booking.BookingId} completed successfully.";


            return RedirectToAction(nameof(Index));
        }
    }
}