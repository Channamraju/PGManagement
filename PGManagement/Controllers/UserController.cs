using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.Models;

namespace PGManagement.Controllers
{
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }


        // =========================================================
        // DASHBOARD
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            // -----------------------------------------------------
            // USER SESSION
            // -----------------------------------------------------

            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }


            // -----------------------------------------------------
            // USER
            // -----------------------------------------------------

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.UserId == userId.Value);

            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }


            // -----------------------------------------------------
            // USER INFORMATION
            // -----------------------------------------------------

            ViewBag.UserName = user.FullName;
            ViewBag.UserImage = user.ProfileImagePath;


            // =====================================================
            // ALL BOOKINGS
            // =====================================================

            var bookings = await _context.Bookings
                .Include(x => x.PG)
                .Include(x => x.Room)
                .Include(x => x.Bed)
                .Include(x => x.Payments)
                .Where(x => x.UserId == userId.Value)
                .OrderByDescending(x => x.BookingDate)
                .ToListAsync();


            // =====================================================
            // BOOKING STATISTICS
            // =====================================================

            var totalBookings = bookings.Count;

            var confirmedBookings = bookings.Count(x =>
                x.Status == "Confirmed");

            var pendingBookings = bookings.Count(x =>
                x.Status == "Pending");

            var completedBookings = bookings.Count(x =>
                x.Status == "Completed");

            var cancelledBookings = bookings.Count(x =>
                x.Status == "Cancelled");


            ViewBag.TotalBookings = totalBookings;
            ViewBag.ConfirmedBookings = confirmedBookings;
            ViewBag.PendingBookings = pendingBookings;
            ViewBag.CompletedBookings = completedBookings;
            ViewBag.CancelledBookings = cancelledBookings;


            // =====================================================
            // TOTAL SUCCESSFUL PAYMENTS
            // =====================================================

            var totalPaid = bookings
                .SelectMany(x => x.Payments ?? new List<Payment>())
                .Where(x => x.Status == "Success")
                .Sum(x => x.Amount);


            ViewBag.TotalPaid = totalPaid;


            // =====================================================
            // CURRENT ACTIVE BOOKING
            // =====================================================

            var currentBooking = bookings
                .Where(x => x.Status == "Confirmed")
                .OrderByDescending(x => x.BookingDate)
                .FirstOrDefault();


            // Reset current booking information
            ViewBag.ConfirmedBookingId = 0;
            ViewBag.PGName = "-";
            ViewBag.RoomNumber = "-";
            ViewBag.BedNumber = "-";
            ViewBag.StayType = "";
            ViewBag.StartDate = null;
            ViewBag.EndDate = null;
            ViewBag.RentAmount = 0m;
            ViewBag.NextPaymentAmount = 0m;
            ViewBag.NextPaymentDate = null;
            ViewBag.CanPayNow = false;


            if (currentBooking != null)
            {
                // -------------------------------------------------
                // BASIC CURRENT BOOKING INFORMATION
                // -------------------------------------------------

                ViewBag.ConfirmedBookingId =
                    currentBooking.BookingId;

                ViewBag.PGName =
                    currentBooking.PG?.PGName ?? "-";

                ViewBag.RoomNumber =
                    currentBooking.Room?.RoomNumber ?? "-";

                ViewBag.BedNumber =
                    currentBooking.Bed?.BedNumber ?? "-";

                ViewBag.StayType =
                    currentBooking.StayType;

                ViewBag.StartDate =
                    currentBooking.StartDate;

                ViewBag.EndDate =
                    currentBooking.EndDate;


                // =================================================
                // SUCCESSFUL PAYMENTS
                // =================================================

                var successfulPayments =
                    currentBooking.Payments?
                        .Where(x => x.Status == "Success")
                        .OrderBy(x => x.PaymentDate)
                        .ToList()
                    ?? new List<Payment>();


                // =================================================
                // MONTHLY STAY
                // =================================================

                if (currentBooking.StayType == "Monthly")
                {
                    // -------------------------------------------------
                    // MONTHLY RENT
                    // -------------------------------------------------

                    var monthlyRent =
                        currentBooking.Room?.MonthlyRent ?? 0m;

                    ViewBag.RentAmount = monthlyRent;


                    // -------------------------------------------------
                    // NUMBER OF SUCCESSFULLY PAID MONTHS
                    // -------------------------------------------------

                    var paidMonths = successfulPayments
                        .Count(x => x.PaymentType == "Monthly");


                    // -------------------------------------------------
                    // NEXT MONTH START
                    //
                    // Example:
                    //
                    // StartDate = 01 Sep
                    // PaidMonths = 0
                    // Next = 01 Sep
                    //
                    // After first payment:
                    // PaidMonths = 1
                    // Next = 01 Oct
                    // -------------------------------------------------

                    var nextPaymentDate =
                        currentBooking.StartDate
                            .AddMonths(paidMonths);


                    // -------------------------------------------------
                    // NEXT MONTH END
                    // -------------------------------------------------

                    var nextPaymentEndDate =
                        nextPaymentDate
                            .AddMonths(1)
                            .AddDays(-1);


                    // -------------------------------------------------
                    // NEXT PAYMENT
                    // -------------------------------------------------

                    ViewBag.NextPaymentAmount =
                        monthlyRent;

                    ViewBag.NextPaymentDate =
                        nextPaymentDate;


                    // -------------------------------------------------
                    // CAN PAY NOW?
                    //
                    // First month:
                    // immediately after owner confirmation.
                    //
                    // Next months:
                    // only when today's date reaches the
                    // next billing date.
                    // -------------------------------------------------

                    ViewBag.CanPayNow =
                        DateTime.Today >= nextPaymentDate;


                    // -------------------------------------------------
                    // Optional information for the view
                    // -------------------------------------------------

                    ViewBag.NextPaymentEndDate =
                        nextPaymentEndDate;
                }


                // =================================================
                // DAILY STAY
                // =================================================

                else if (currentBooking.StayType == "Daily")
                {
                    // -------------------------------------------------
                    // DAILY RENT
                    // -------------------------------------------------

                    var dailyRent =
                        currentBooking.Room?.DailyRent ?? 0m;

                    ViewBag.RentAmount = dailyRent;


                    // -------------------------------------------------
                    // CHECK WHETHER DAILY PAYMENT WAS ALREADY MADE
                    // -------------------------------------------------

                    var dailyPaymentExists =
                        successfulPayments.Any(x =>
                            x.PaymentType == "Daily");


                    if (!dailyPaymentExists)
                    {
                        // -------------------------------------------------
                        // CALCULATE NUMBER OF DAYS
                        // -------------------------------------------------

                        var numberOfDays = 0;

                        if (currentBooking.EndDate.HasValue)
                        {
                            numberOfDays =
                                (currentBooking.EndDate.Value.Date -
                                 currentBooking.StartDate.Date).Days + 1;
                        }


                        // -------------------------------------------------
                        // DAILY TOTAL
                        // -------------------------------------------------

                        var dailyTotal =
                            dailyRent * numberOfDays;


                        ViewBag.NextPaymentAmount =
                            dailyTotal;


                        ViewBag.NextPaymentDate =
                            currentBooking.StartDate;


                        ViewBag.CanPayNow = true;
                    }
                    else
                    {
                        // -------------------------------------------------
                        // DAILY PAYMENT ALREADY COMPLETED
                        // -------------------------------------------------

                        ViewBag.NextPaymentAmount = 0m;
                        ViewBag.NextPaymentDate = null;
                        ViewBag.CanPayNow = false;
                    }
                }
            }


            // =====================================================
            // RETURN VIEW
            // =====================================================

            return View();
        }



        // =========================================================
        // PROFILE
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }


            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.UserId == userId.Value);


            if (user == null)
            {
                return NotFound();
            }


            return View(user);
        }



        // =========================================================
        // EDIT PROFILE - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }


            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.UserId == userId.Value);


            if (user == null)
            {
                return NotFound();
            }


            return View(user);
        }



        // =========================================================
        // EDIT PROFILE - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(
            User model,
            IFormFile? ProfileImage)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }


            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.UserId == userId.Value);


            if (user == null)
            {
                return NotFound();
            }


            // -----------------------------------------------------
            // REMOVE MODEL VALIDATION FOR DATABASE-ONLY FIELDS
            // -----------------------------------------------------

            ModelState.Remove("PasswordHash");
            ModelState.Remove("ProfileImagePath");
            ModelState.Remove("AadhaarImagePath");


            if (!ModelState.IsValid)
            {
                return View(model);
            }


            // -----------------------------------------------------
            // UPDATE BASIC INFORMATION
            // -----------------------------------------------------

            user.FullName = model.FullName;
            user.Phone = model.Phone;
            user.Address = model.Address;


            // -----------------------------------------------------
            // PROFILE IMAGE
            // -----------------------------------------------------

            if (ProfileImage != null &&
                ProfileImage.Length > 0)
            {
                var uploadsFolder =
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "uploads",
                        "profiles");


                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }


                var extension =
                    Path.GetExtension(ProfileImage.FileName);


                var fileName =
                    Guid.NewGuid().ToString() + extension;


                var filePath =
                    Path.Combine(
                        uploadsFolder,
                        fileName);


                using (var stream =
                       new FileStream(
                           filePath,
                           FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(stream);
                }


                user.ProfileImagePath =
                    "/uploads/profiles/" + fileName;
            }


            await _context.SaveChangesAsync();


            // -----------------------------------------------------
            // UPDATE SESSION
            // -----------------------------------------------------

            HttpContext.Session.SetString(
                "UserName",
                user.FullName);


            if (!string.IsNullOrWhiteSpace(
                user.ProfileImagePath))
            {
                HttpContext.Session.SetString(
                    "UserImage",
                    user.ProfileImagePath);
            }


            TempData["Success"] =
                "Profile updated successfully.";


            return RedirectToAction(nameof(Profile));
        }
    }
}