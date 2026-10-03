using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.Models;

namespace PGManagement.Controllers
{
    public class OwnerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OwnerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // OWNER LOGIN CHECK
        // =========================================================

        private bool IsOwner(out int ownerId)
        {
            var sessionOwnerId =
                HttpContext.Session.GetInt32("OwnerId");

            var userType =
                HttpContext.Session.GetString("UserType");

            if (sessionOwnerId == null ||
                userType != "OWNER")
            {
                ownerId = 0;
                return false;
            }

            ownerId = sessionOwnerId.Value;

            return true;
        }

        // =========================================================
        // OWNER DASHBOARD
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            // =====================================================
            // PG COUNT
            // =====================================================

            var pgCount =
                await _context.PGs
                    .CountAsync(x =>
                        x.OwnerId == ownerId);

            // =====================================================
            // ROOM COUNT
            // =====================================================

            var roomCount =
                await _context.Rooms
                    .CountAsync(x =>
                        x.PG != null &&
                        x.PG.OwnerId == ownerId);

            // =====================================================
            // BED COUNTS
            // =====================================================

            var ownerBeds =
                _context.Beds
                    .Where(x =>
                        x.Room != null &&
                        x.Room.PG != null &&
                        x.Room.PG.OwnerId == ownerId);

            var totalBeds =
                await ownerBeds.CountAsync();

            var availableBeds =
                await ownerBeds.CountAsync(x =>
                    x.Status == "Available");

            var occupiedBeds =
                await ownerBeds.CountAsync(x =>
                    x.Status == "Occupied");

            // =====================================================
            // OWNER BOOKINGS QUERY
            // =====================================================

            var ownerBookings =
                _context.Bookings
                    .Where(x =>
                        x.PG != null &&
                        x.PG.OwnerId == ownerId);

            // =====================================================
            // BOOKING COUNTS
            // =====================================================

            var totalBookings =
                await ownerBookings.CountAsync();

            var pendingBookings =
                await ownerBookings.CountAsync(x =>
                    x.Status == "Pending");

            var confirmedBookings =
                await ownerBookings.CountAsync(x =>
                    x.Status == "Confirmed");

            var completedBookings =
                await ownerBookings.CountAsync(x =>
                    x.Status == "Completed");

            var cancelledBookings =
                await ownerBookings.CountAsync(x =>
                    x.Status == "Cancelled");

            // =====================================================
            // STAY TYPE COUNTS
            // =====================================================

            var monthlyBookings =
                await ownerBookings.CountAsync(x =>
                    x.StayType == "Monthly");

            var dailyBookings =
                await ownerBookings.CountAsync(x =>
                    x.StayType == "Daily");

            // =====================================================
            // SUCCESSFUL PAYMENTS
            // =====================================================

            var successfulPayments =
                _context.Payments
                    .Where(x =>
                        x.Status == "Success" &&
                        x.Booking != null &&
                        x.Booking.PG != null &&
                        x.Booking.PG.OwnerId == ownerId);

            // =====================================================
            // TOTAL REVENUE
            // =====================================================

            var totalPayments =
                await successfulPayments
                    .SumAsync(x =>
                        (decimal?)x.Amount) ?? 0m;

            // =====================================================
            // MONTHLY REVENUE
            // =====================================================

            var monthlyPaymentTotal =
                await successfulPayments
                    .Where(x =>
                        x.PaymentType == "Monthly")
                    .SumAsync(x =>
                        (decimal?)x.Amount) ?? 0m;

            // =====================================================
            // DAILY REVENUE
            // =====================================================

            var dailyPaymentTotal =
                await successfulPayments
                    .Where(x =>
                        x.PaymentType == "Daily")
                    .SumAsync(x =>
                        (decimal?)x.Amount) ?? 0m;

            // =====================================================
            // PAYMENT COUNT
            // =====================================================

            var paymentCount =
                await successfulPayments.CountAsync();

            // =====================================================
            // TODAY PAYMENTS
            // =====================================================

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var todayPayments =
                await successfulPayments
                    .Where(x =>
                        x.PaymentDate >= today &&
                        x.PaymentDate < tomorrow)
                    .SumAsync(x =>
                        (decimal?)x.Amount) ?? 0m;

            // =====================================================
            // CURRENT MONTH PAYMENTS
            // =====================================================

            var monthStart =
                new DateTime(
                    today.Year,
                    today.Month,
                    1);

            var nextMonthStart =
                monthStart.AddMonths(1);

            var currentMonthPayments =
                await successfulPayments
                    .Where(x =>
                        x.PaymentDate >= monthStart &&
                        x.PaymentDate < nextMonthStart)
                    .SumAsync(x =>
                        (decimal?)x.Amount) ?? 0m;

            // =====================================================
            // CONFIRMED BOOKINGS
            // =====================================================

            var confirmedBookingsList =
                await _context.Bookings
                    .Include(x => x.Payments)
                    .Include(x => x.Room)
                    .Include(x => x.PG)
                    .Where(x =>
                        x.Status == "Confirmed" &&
                        x.PG != null &&
                        x.PG.OwnerId == ownerId)
                    .ToListAsync();

            // =====================================================
            // PENDING PAYMENT AMOUNT
            // =====================================================

            decimal pendingPaymentAmount = 0m;

            foreach (var booking in confirmedBookingsList)
            {
                var payments =
                    booking.Payments?
                        .Where(x =>
                            x.Status == "Success")
                        .OrderBy(x => x.PaymentDate)
                        .ToList()
                    ?? new List<Payment>();

                // =================================================
                // MONTHLY BOOKING
                // =================================================

                if (booking.StayType == "Monthly")
                {
                    var monthlyPayments =
                        payments
                            .Where(x =>
                                x.PaymentType == "Monthly")
                            .OrderBy(x => x.FromDate)
                            .ToList();

                    decimal monthlyRent =
                        booking.Room?.MonthlyRent ?? 0m;

                    if (monthlyRent <= 0)
                    {
                        continue;
                    }

                    int paidMonths =
                        monthlyPayments.Count;

                    DateTime nextPaymentDate =
                        booking.StartDate
                            .Date
                            .AddMonths(paidMonths);

                    // Payment is due when the next period starts.
                    if (today >= nextPaymentDate)
                    {
                        pendingPaymentAmount += monthlyRent;
                    }
                }

                // =================================================
                // DAILY BOOKING
                // =================================================

                else if (booking.StayType == "Daily")
                {
                    bool paymentCompleted =
                        payments.Any(x =>
                            x.PaymentType == "Daily");

                    if (!paymentCompleted)
                    {
                        pendingPaymentAmount +=
                            booking.TotalAmount;
                    }
                }
            }

            // =====================================================
            // RECENT BOOKINGS
            // =====================================================

            var recentBookings =
                await _context.Bookings
                    .AsNoTracking()
                    .Include(x => x.User)
                    .Include(x => x.PG)
                    .Include(x => x.Room)
                    .Include(x => x.Bed)
                    .Include(x => x.Payments)
                    .Where(x =>
                        x.PG != null &&
                        x.PG.OwnerId == ownerId)
                    .OrderByDescending(x =>
                        x.BookingDate)
                    .Take(5)
                    .ToListAsync();

            // =====================================================
            // OWNER INFORMATION
            // =====================================================

            var owner =
                await _context.Owners
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.OwnerId == ownerId);

            if (owner != null)
            {
                HttpContext.Session.SetString(
                    "UserName",
                    owner.FullName ?? "Owner");

                if (!string.IsNullOrWhiteSpace(
                    owner.ProfileImagePath))
                {
                    HttpContext.Session.SetString(
                        "UserImage",
                        owner.ProfileImagePath);
                }
                else
                {
                    HttpContext.Session.Remove(
                        "UserImage");
                }
            }

            // =====================================================
            // SEND DATA TO VIEW
            // =====================================================

            ViewBag.PGCount =
                pgCount;

            ViewBag.RoomCount =
                roomCount;

            ViewBag.TotalBeds =
                totalBeds;

            ViewBag.AvailableBeds =
                availableBeds;

            ViewBag.OccupiedBeds =
                occupiedBeds;

            ViewBag.TotalBookings =
                totalBookings;

            ViewBag.PendingBookings =
                pendingBookings;

            ViewBag.ConfirmedBookings =
                confirmedBookings;

            ViewBag.CompletedBookings =
                completedBookings;

            ViewBag.CancelledBookings =
                cancelledBookings;

            ViewBag.MonthlyBookings =
                monthlyBookings;

            ViewBag.DailyBookings =
                dailyBookings;

            ViewBag.TotalPayments =
                totalPayments;

            ViewBag.MonthlyPayments =
                monthlyPaymentTotal;

            ViewBag.DailyPayments =
                dailyPaymentTotal;

            ViewBag.MonthlyPaymentTotal =
                monthlyPaymentTotal;

            ViewBag.DailyPaymentTotal =
                dailyPaymentTotal;

            ViewBag.PaymentCount =
                paymentCount;

            ViewBag.TodayPayments =
                todayPayments;

            ViewBag.CurrentMonthPayments =
                currentMonthPayments;

            ViewBag.PendingPaymentAmount =
                pendingPaymentAmount;

            ViewBag.RecentBookings =
                recentBookings;

            return View();
        }

        // =========================================================
        // OWNER PROFILE
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var owner =
                await _context.Owners
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.OwnerId == ownerId);

            if (owner == null)
            {
                return NotFound();
            }

            return View(owner);
        }

        // =========================================================
        // EDIT PROFILE - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var owner =
                await _context.Owners
                    .FirstOrDefaultAsync(x =>
                        x.OwnerId == ownerId);

            if (owner == null)
            {
                return NotFound();
            }

            return View(owner);
        }

        // =========================================================
        // EDIT PROFILE - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(
            Owner model,
            IFormFile? ProfileImage)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var owner =
                await _context.Owners
                    .FirstOrDefaultAsync(x =>
                        x.OwnerId == ownerId);

            if (owner == null)
            {
                return NotFound();
            }

            // Password is not edited here.
            ModelState.Remove("PasswordHash");

            // Email is not edited here.
            ModelState.Remove("Email");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // =====================================================
            // BASIC INFORMATION
            // =====================================================

            owner.FullName =
                model.FullName;

            owner.Phone =
                model.Phone;

            owner.Address =
                model.Address;

            // =====================================================
            // PROFILE IMAGE
            // =====================================================

            if (ProfileImage != null &&
                ProfileImage.Length > 0)
            {
                var allowedExtensions =
                    new[]
                    {
                        ".jpg",
                        ".jpeg",
                        ".png",
                        ".webp"
                    };

                var extension =
                    Path.GetExtension(
                        ProfileImage.FileName)
                    .ToLowerInvariant();

                if (!allowedExtensions.Contains(
                    extension))
                {
                    ModelState.AddModelError(
                        "ProfileImage",
                        "Only JPG, JPEG, PNG and WEBP images are allowed.");

                    return View(model);
                }

                // Maximum 5 MB
                if (ProfileImage.Length >
                    5 * 1024 * 1024)
                {
                    ModelState.AddModelError(
                        "ProfileImage",
                        "Profile image must be less than 5 MB.");

                    return View(model);
                }

                // =================================================
                // DELETE OLD IMAGE
                // =================================================

                if (!string.IsNullOrWhiteSpace(
                    owner.ProfileImagePath))
                {
                    var oldRelativePath =
                        owner.ProfileImagePath
                            .TrimStart('/')
                            .Replace(
                                "/",
                                Path.DirectorySeparatorChar
                                    .ToString());

                    var oldImagePath =
                        Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "wwwroot",
                            oldRelativePath);

                    if (System.IO.File.Exists(
                        oldImagePath))
                    {
                        System.IO.File.Delete(
                            oldImagePath);
                    }
                }

                // =================================================
                // OWNER UPLOAD DIRECTORY
                // =================================================

                var uploadFolder =
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "uploads",
                        "owners");

                if (!Directory.Exists(
                    uploadFolder))
                {
                    Directory.CreateDirectory(
                        uploadFolder);
                }

                // =================================================
                // UNIQUE FILE NAME
                // =================================================

                var fileName =
                    Guid.NewGuid()
                        .ToString("N") +
                    extension;

                var filePath =
                    Path.Combine(
                        uploadFolder,
                        fileName);

                // =================================================
                // SAVE IMAGE
                // =================================================

                await using (
                    var stream =
                        new FileStream(
                            filePath,
                            FileMode.Create))
                {
                    await ProfileImage
                        .CopyToAsync(stream);
                }

                owner.ProfileImagePath =
                    "/uploads/owners/" +
                    fileName;
            }

            // =====================================================
            // SAVE DATABASE
            // =====================================================

            await _context.SaveChangesAsync();

            // =====================================================
            // UPDATE SESSION
            // =====================================================

            HttpContext.Session.SetString(
                "UserName",
                owner.FullName ?? "Owner");

            if (!string.IsNullOrWhiteSpace(
                owner.ProfileImagePath))
            {
                HttpContext.Session.SetString(
                    "UserImage",
                    owner.ProfileImagePath);
            }
            else
            {
                HttpContext.Session.Remove(
                    "UserImage");
            }

            TempData["Success"] =
                "Profile updated successfully.";

            return RedirectToAction(
                nameof(Profile));
        }
    }
}