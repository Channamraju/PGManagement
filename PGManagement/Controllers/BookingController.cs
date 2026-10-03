using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.Models;

namespace PGManagement.Controllers
{
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // BOOK BED
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            int pgId,
            int roomId,
            int bedId,
            string stayType,
            DateTime startDate,
            DateTime? endDate)
        {
            // -----------------------------------------------------
            // CHECK USER LOGIN
            // -----------------------------------------------------

            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------------------------------
            // VALIDATE STAY TYPE
            // -----------------------------------------------------

            if (stayType != "Monthly" && stayType != "Daily")
            {
                TempData["Error"] = "Please select Monthly or Daily stay.";

                return RedirectToAction(
                    "Details",
                    "UserPG",
                    new { id = pgId });
            }

            // -----------------------------------------------------
            // VALIDATE START DATE
            // -----------------------------------------------------

            if (startDate.Date < DateTime.Today)
            {
                TempData["Error"] =
                    "Start date cannot be before today.";

                return RedirectToAction(
                    "Details",
                    "UserPG",
                    new { id = pgId });
            }

            // -----------------------------------------------------
            // DAILY STAY VALIDATION
            // -----------------------------------------------------

            if (stayType == "Daily")
            {
                if (endDate == null)
                {
                    TempData["Error"] =
                        "Please select an end date for daily stay.";

                    return RedirectToAction(
                        "Details",
                        "UserPG",
                        new { id = pgId });
                }

                if (endDate.Value.Date < startDate.Date)
                {
                    TempData["Error"] =
                        "End date must be after or equal to start date.";

                    return RedirectToAction(
                        "Details",
                        "UserPG",
                        new { id = pgId });
                }
            }

            // -----------------------------------------------------
            // MONTHLY STAY
            // -----------------------------------------------------

            if (stayType == "Monthly")
            {
                // Monthly booking does not need an end date.
                endDate = null;
            }

            // -----------------------------------------------------
            // CHECK PG
            // -----------------------------------------------------

            var pg = await _context.PGs
                .FirstOrDefaultAsync(x =>
                    x.PGId == pgId &&
                    x.Status == "Approved");

            if (pg == null)
            {
                TempData["Error"] =
                    "PG is not available.";

                return RedirectToAction(
                    "Index",
                    "UserPG");
            }

            // -----------------------------------------------------
            // CHECK ROOM
            // -----------------------------------------------------

            var room = await _context.Rooms
                .FirstOrDefaultAsync(x =>
                    x.RoomId == roomId &&
                    x.PGId == pgId);

            if (room == null)
            {
                TempData["Error"] =
                    "Room not found.";

                return RedirectToAction(
                    "Details",
                    "UserPG",
                    new { id = pgId });
            }

            // -----------------------------------------------------
            // CHECK BED
            // -----------------------------------------------------

            var bed = await _context.Beds
                .FirstOrDefaultAsync(x =>
                    x.BedId == bedId &&
                    x.RoomId == roomId);

            if (bed == null)
            {
                TempData["Error"] =
                    "Bed not found.";

                return RedirectToAction(
                    "Details",
                    "UserPG",
                    new { id = pgId });
            }

            // -----------------------------------------------------
            // CHECK BED STATUS
            // -----------------------------------------------------

            if (bed.Status != "Available")
            {
                TempData["Error"] =
                    "This bed is no longer available.";

                return RedirectToAction(
                    "Details",
                    "UserPG",
                    new { id = pgId });
            }

            // -----------------------------------------------------
            // CHECK IF BED IS ALREADY BOOKED
            // -----------------------------------------------------

            var alreadyBooked =
                await _context.Bookings.AnyAsync(x =>
                    x.BedId == bedId &&
                    (x.Status == "Pending" ||
                     x.Status == "Confirmed"));

            if (alreadyBooked)
            {
                TempData["Error"] =
                    "This bed has already been booked.";

                return RedirectToAction(
                    "Details",
                    "UserPG",
                    new { id = pgId });
            }

            // -----------------------------------------------------
            // CHECK USER ALREADY HAS BOOKING FOR THIS BED
            // -----------------------------------------------------

            var userAlreadyBooked =
                await _context.Bookings.AnyAsync(x =>
                    x.UserId == userId.Value &&
                    x.BedId == bedId &&
                    (x.Status == "Pending" ||
                     x.Status == "Confirmed"));

            if (userAlreadyBooked)
            {
                TempData["Error"] =
                    "You already have a booking for this bed.";

                return RedirectToAction(
                    nameof(MyBookings));
            }

            // -----------------------------------------------------
            // CALCULATE TOTAL AMOUNT
            // -----------------------------------------------------

            decimal totalAmount;

            if (stayType == "Monthly")
            {
                // One month amount
                totalAmount = room.MonthlyRent;
            }
            else
            {
                // Daily booking
                int numberOfDays =
                    (endDate!.Value.Date - startDate.Date).Days + 1;

                totalAmount =
                    room.DailyRent * numberOfDays;
            }

            // -----------------------------------------------------
            // CREATE BOOKING
            // -----------------------------------------------------

            var booking = new Booking
            {
                UserId = userId.Value,

                PGId = pgId,

                RoomId = roomId,

                BedId = bedId,

                BookingDate = DateTime.Now,

                StayType = stayType,

                StartDate = startDate.Date,

                EndDate = endDate?.Date,

                TotalAmount = totalAmount,

                Status = "Pending"
            };

            _context.Bookings.Add(booking);

            await _context.SaveChangesAsync();

            // -----------------------------------------------------
            // DO NOT TAKE PAYMENT HERE
            //
            // OWNER MUST APPROVE THE BOOKING FIRST.
            // -----------------------------------------------------

            TempData["Success"] =
                $"Booking request submitted successfully. " +
                $"Stay type: {stayType}. " +
                $"Amount: ₹{totalAmount:N2}. " +
                $"Please wait for owner approval.";

            return RedirectToAction(nameof(MyBookings));
        }


        // =========================================================
        // MY BOOKINGS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            // -----------------------------------------------------
            // CHECK USER LOGIN
            // -----------------------------------------------------

            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------------------------------
            // GET USER BOOKINGS
            // -----------------------------------------------------

            var bookings = await _context.Bookings

                .Include(x => x.PG)

                .Include(x => x.Room)

                .Include(x => x.Bed)

                // IMPORTANT:
                // Booking can now have multiple payments
                .Include(x => x.Payments)

                .Where(x => x.UserId == userId.Value)

                .OrderByDescending(x => x.BookingDate)

                .ToListAsync();

            return View(bookings);
        }


        // =========================================================
        // CANCEL BOOKING
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            // -----------------------------------------------------
            // CHECK USER LOGIN
            // -----------------------------------------------------

            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------------------------------
            // FIND BOOKING
            // -----------------------------------------------------

            var booking = await _context.Bookings

                .Include(x => x.Bed)

                .Include(x => x.Room)

                .Include(x => x.Payments)

                .FirstOrDefaultAsync(x =>
                    x.BookingId == id &&
                    x.UserId == userId.Value);

            if (booking == null)
            {
                TempData["Error"] =
                    "Booking not found.";

                return RedirectToAction(
                    nameof(MyBookings));
            }

            // -----------------------------------------------------
            // CHECK STATUS
            // -----------------------------------------------------

            if (booking.Status == "Cancelled")
            {
                TempData["Error"] =
                    "Booking is already cancelled.";

                return RedirectToAction(
                    nameof(MyBookings));
            }

            if (booking.Status == "Completed")
            {
                TempData["Error"] =
                    "Completed bookings cannot be cancelled.";

                return RedirectToAction(
                    nameof(MyBookings));
            }

            // -----------------------------------------------------
            // CHECK IF CONFIRMED
            // -----------------------------------------------------

            bool wasConfirmed =
                booking.Status == "Confirmed";

            booking.Status = "Cancelled";

            // -----------------------------------------------------
            // IF CONFIRMED:
            // MAKE BED AVAILABLE AGAIN
            // -----------------------------------------------------

            if (wasConfirmed)
            {
                // -------------------------------------------------
                // MAKE BED AVAILABLE
                // -------------------------------------------------

                if (booking.Bed != null)
                {
                    booking.Bed.Status = "Available";
                }

                // -------------------------------------------------
                // UPDATE ROOM
                // -------------------------------------------------

                if (booking.Room != null)
                {
                    booking.Room.AvailableBeds++;

                    if (booking.Room.Status == "Full")
                    {
                        booking.Room.Status = "Available";
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Booking cancelled successfully.";

            return RedirectToAction(
                nameof(MyBookings));
        }


        // =========================================================
        // DELETE CANCELLED BOOKING
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            // -----------------------------------------------------
            // CHECK USER LOGIN
            // -----------------------------------------------------

            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------------------------------
            // FIND BOOKING
            // -----------------------------------------------------

            var booking = await _context.Bookings

                .Include(x => x.Payments)

                .FirstOrDefaultAsync(x =>
                    x.BookingId == id &&
                    x.UserId == userId.Value);

            if (booking == null)
            {
                TempData["Error"] =
                    "Booking not found.";

                return RedirectToAction(
                    nameof(MyBookings));
            }

            // -----------------------------------------------------
            // ONLY CANCELLED BOOKING CAN BE DELETED
            // -----------------------------------------------------

            if (booking.Status != "Cancelled")
            {
                TempData["Error"] =
                    "Only cancelled bookings can be deleted.";

                return RedirectToAction(
                    nameof(MyBookings));
            }

            // -----------------------------------------------------
            // REMOVE ALL PAYMENTS
            // -----------------------------------------------------

            if (booking.Payments != null &&
                booking.Payments.Any())
            {
                _context.Payments.RemoveRange(
                    booking.Payments);
            }

            // -----------------------------------------------------
            // DELETE BOOKING
            // -----------------------------------------------------

            _context.Bookings.Remove(booking);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Booking deleted successfully.";

            return RedirectToAction(
                nameof(MyBookings));
        }
    }
}