using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.Models;

namespace PGManagement.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // CREATE PAYMENT - GET
        // URL:
        // /Payment/Create?bookingId=1
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Create(int? bookingId)
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
            // CHECK BOOKING ID
            // -----------------------------------------------------

            if (bookingId == null)
            {
                TempData["Error"] =
                    "Please select a booking before making payment.";

                return RedirectToAction(
                    "MyBookings",
                    "Booking");
            }

            // -----------------------------------------------------
            // GET BOOKING
            // -----------------------------------------------------

            var booking = await _context.Bookings

                .Include(x => x.PG)

                .Include(x => x.Room)

                .Include(x => x.Bed)

                .Include(x => x.Payments)

                .FirstOrDefaultAsync(x =>
                    x.BookingId == bookingId.Value &&
                    x.UserId == userId.Value);

            if (booking == null)
            {
                TempData["Error"] =
                    "Booking not found.";

                return RedirectToAction(
                    "MyBookings",
                    "Booking");
            }

            // -----------------------------------------------------
            // BOOKING MUST BE CONFIRMED
            // -----------------------------------------------------

            if (booking.Status != "Confirmed")
            {
                TempData["Error"] =
                    "Payment is available only after your booking is confirmed.";

                return RedirectToAction(
                    "MyBookings",
                    "Booking");
            }

            // -----------------------------------------------------
            // CHECK PAYMENT
            // -----------------------------------------------------

            var paymentInfo = CalculatePayment(booking);

            if (!paymentInfo.CanPay)
            {
                TempData["Info"] = paymentInfo.Message;

                return RedirectToAction(nameof(History));
            }

            // -----------------------------------------------------
            // SEND INFORMATION TO VIEW
            // -----------------------------------------------------

            ViewBag.Amount = paymentInfo.Amount;

            ViewBag.PaymentType = paymentInfo.PaymentType;

            ViewBag.FromDate = paymentInfo.FromDate;

            ViewBag.ToDate = paymentInfo.ToDate;

            ViewBag.BillingMonth = paymentInfo.BillingMonth;

            ViewBag.PGName =
                booking.PG?.PGName ?? "PG";

            ViewBag.RoomNumber =
                booking.Room?.RoomNumber ?? "-";

            ViewBag.BedNumber =
                booking.Bed?.BedNumber ?? "-";

            ViewBag.StayType =
                booking.StayType;

            return View(booking);
        }


        // =========================================================
        // CREATE PAYMENT - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            int bookingId,
            string paymentMethod)
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
            // VALIDATE PAYMENT METHOD
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(paymentMethod))
            {
                TempData["Error"] =
                    "Please select a payment method.";

                return RedirectToAction(
                    nameof(Create),
                    new { bookingId });
            }

            // -----------------------------------------------------
            // GET BOOKING
            // -----------------------------------------------------

            var booking = await _context.Bookings

                .Include(x => x.Room)

                .Include(x => x.Payments)

                .FirstOrDefaultAsync(x =>
                    x.BookingId == bookingId &&
                    x.UserId == userId.Value);

            if (booking == null)
            {
                TempData["Error"] =
                    "Booking not found.";

                return RedirectToAction(
                    "MyBookings",
                    "Booking");
            }

            // -----------------------------------------------------
            // CHECK BOOKING STATUS
            // -----------------------------------------------------

            if (booking.Status != "Confirmed")
            {
                TempData["Error"] =
                    "This booking is not confirmed yet.";

                return RedirectToAction(
                    "MyBookings",
                    "Booking");
            }

            // -----------------------------------------------------
            // CALCULATE CURRENT PAYMENT
            // -----------------------------------------------------

            var paymentInfo = CalculatePayment(booking);

            if (!paymentInfo.CanPay)
            {
                TempData["Info"] =
                    paymentInfo.Message;

                return RedirectToAction(nameof(History));
            }

            // -----------------------------------------------------
            // VALIDATE AMOUNT
            // -----------------------------------------------------

            if (paymentInfo.Amount <= 0)
            {
                TempData["Error"] =
                    "Invalid payment amount.";

                return RedirectToAction(
                    nameof(Create),
                    new { bookingId });
            }

            // -----------------------------------------------------
            // GENERATE TRANSACTION ID
            // -----------------------------------------------------

            string transactionId =
                "TXN" +
                DateTime.Now.ToString(
                    "yyyyMMddHHmmssfff");

            // -----------------------------------------------------
            // CREATE PAYMENT
            // -----------------------------------------------------

            var payment = new Payment
            {
                BookingId = booking.BookingId,

                UserId = userId.Value,

                Amount = paymentInfo.Amount,

                PaymentMethod = paymentMethod,

                TransactionId = transactionId,

                PaymentDate = DateTime.Now,

                Status = "Success",

                PaymentType =
                    paymentInfo.PaymentType,

                BillingMonth =
                    paymentInfo.BillingMonth,

                FromDate =
                    paymentInfo.FromDate,

                ToDate =
                    paymentInfo.ToDate
            };

            _context.Payments.Add(payment);

            await _context.SaveChangesAsync();

            // -----------------------------------------------------
            // PAYMENT SUCCESS
            // -----------------------------------------------------

            return RedirectToAction(
                nameof(PaymentSuccess),
                new
                {
                    id = payment.PaymentId
                });
        }


        // =========================================================
        // CALCULATE PAYMENT
        // =========================================================

        private PaymentCalculation CalculatePayment(
            Booking booking)
        {
            // =====================================================
            // MONTHLY BOOKING
            // =====================================================

            if (booking.StayType == "Monthly")
            {
                decimal monthlyRent =
                    booking.Room?.MonthlyRent ?? 0;

                if (monthlyRent <= 0)
                {
                    return new PaymentCalculation
                    {
                        CanPay = false,
                        Message =
                            "Monthly rent is not configured for this room."
                    };
                }

                // -------------------------------------------------
                // GET SUCCESSFUL MONTHLY PAYMENTS
                // -------------------------------------------------

                var successfulPayments =
                    booking.Payments?

                        .Where(x =>
                            x.Status == "Success" &&
                            x.PaymentType == "Monthly")

                        .OrderBy(x => x.FromDate)

                        .ToList()
                    ?? new List<Payment>();

                // -------------------------------------------------
                // FIRST MONTH
                // -------------------------------------------------

                DateTime firstMonthStart =
                    booking.StartDate.Date;

                // -------------------------------------------------
                // DETERMINE NEXT MONTH NUMBER
                // -------------------------------------------------

                int paidMonths =
                    successfulPayments.Count;

                DateTime nextMonthStart =
                    firstMonthStart.AddMonths(
                        paidMonths);

                // -------------------------------------------------
                // CURRENT MONTH PERIOD
                //
                // Example:
                //
                // Start: 08 Sep
                // End:   07 Oct
                //
                // Next:
                //
                // Start: 08 Oct
                // End:   07 Nov
                // -------------------------------------------------

                DateTime nextMonthEnd =
                    nextMonthStart
                        .AddMonths(1)
                        .AddDays(-1);

                // -------------------------------------------------
                // IMPORTANT:
                //
                // User can pay first month immediately after
                // booking confirmation.
                //
                // For next months, payment becomes available
                // when the previous month has completed.
                // -------------------------------------------------

                if (paidMonths > 0)
                {
                    if (DateTime.Today <
                        nextMonthStart)
                    {
                        DateTime previousEnd =
                            nextMonthStart.AddDays(-1);

                        return new PaymentCalculation
                        {
                            CanPay = false,

                            Message =
                                $"Your current month is already paid. " +
                                $"Next payment will be available from " +
                                $"{nextMonthStart:dd MMM yyyy}."
                        };
                    }
                }

                // -------------------------------------------------
                // CHECK IF SAME PERIOD ALREADY PAID
                // -------------------------------------------------

                bool alreadyPaid =
                    successfulPayments.Any(x =>
                        x.FromDate.HasValue &&
                        x.ToDate.HasValue &&
                        x.FromDate.Value.Date ==
                            nextMonthStart.Date &&
                        x.ToDate.Value.Date ==
                            nextMonthEnd.Date);

                if (alreadyPaid)
                {
                    return new PaymentCalculation
                    {
                        CanPay = false,

                        Message =
                            "This month's payment has already been completed."
                    };
                }

                // -------------------------------------------------
                // MONTHLY PAYMENT
                // -------------------------------------------------

                return new PaymentCalculation
                {
                    CanPay = true,

                    Amount = monthlyRent,

                    PaymentType = "Monthly",

                    BillingMonth = nextMonthStart,

                    FromDate = nextMonthStart,

                    ToDate = nextMonthEnd
                };
            }


            // =====================================================
            // DAILY BOOKING
            // =====================================================

            if (booking.StayType == "Daily")
            {
                decimal dailyRent =
                    booking.Room?.DailyRent ?? 0;

                if (dailyRent <= 0)
                {
                    return new PaymentCalculation
                    {
                        CanPay = false,

                        Message =
                            "Daily rent is not configured for this room."
                    };
                }

                // -------------------------------------------------
                // CHECK IF DAILY BOOKING IS ALREADY PAID
                // -------------------------------------------------

                bool alreadyPaid =
                    booking.Payments?
                        .Any(x =>
                            x.Status == "Success" &&
                            x.PaymentType == "Daily")
                    ?? false;

                if (alreadyPaid)
                {
                    return new PaymentCalculation
                    {
                        CanPay = false,

                        Message =
                            "Payment for this daily booking has already been completed."
                    };
                }

                // -------------------------------------------------
                // DAILY DATES
                // -------------------------------------------------

                if (!booking.EndDate.HasValue)
                {
                    return new PaymentCalculation
                    {
                        CanPay = false,

                        Message =
                            "Daily booking end date is missing."
                    };
                }

                DateTime fromDate =
                    booking.StartDate.Date;

                DateTime toDate =
                    booking.EndDate.Value.Date;

                int numberOfDays =
                    (toDate - fromDate).Days + 1;

                if (numberOfDays <= 0)
                {
                    return new PaymentCalculation
                    {
                        CanPay = false,

                        Message =
                            "Invalid booking dates."
                    };

                }

                decimal amount =
                    dailyRent * numberOfDays;

                // -------------------------------------------------
                // DAILY PAYMENT
                // -------------------------------------------------

                return new PaymentCalculation
                {
                    CanPay = true,

                    Amount = amount,

                    PaymentType = "Daily",

                    FromDate = fromDate,

                    ToDate = toDate
                };
            }


            // =====================================================
            // INVALID STAY TYPE
            // =====================================================

            return new PaymentCalculation
            {
                CanPay = false,

                Message =
                    "Invalid booking stay type."
            };
        }


        // =========================================================
        // PAYMENT SUCCESS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> PaymentSuccess(int id)
        {
            // -----------------------------------------------------
            // CHECK LOGIN
            // -----------------------------------------------------

            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------------------------------
            // GET PAYMENT
            // -----------------------------------------------------

            var payment = await _context.Payments

                .Include(x => x.Booking)
                .ThenInclude(x => x.PG)

                .Include(x => x.Booking)
                .ThenInclude(x => x.Room)

                .Include(x => x.Booking)
                .ThenInclude(x => x.Bed)

                .FirstOrDefaultAsync(x =>
                    x.PaymentId == id &&
                    x.UserId == userId.Value);

            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }


        // =========================================================
        // PAYMENT HISTORY
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> History()
        {
            // -----------------------------------------------------
            // CHECK LOGIN
            // -----------------------------------------------------

            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------------------------------
            // GET ALL PAYMENTS
            // -----------------------------------------------------

            var payments = await _context.Payments

                .Include(x => x.Booking)
                .ThenInclude(x => x.PG)

                .Include(x => x.Booking)
                .ThenInclude(x => x.Room)

                .Include(x => x.Booking)
                .ThenInclude(x => x.Bed)

                .Where(x =>
                    x.UserId == userId.Value)

                .OrderByDescending(x => x.PaymentDate)

                .ToListAsync();

            return View(payments);
        }


        // =========================================================
        // PAYMENT DETAILS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            // -----------------------------------------------------
            // CHECK LOGIN
            // -----------------------------------------------------

            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");

            if (userId == null || userType != "USER")
            {
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------------------------------
            // GET PAYMENT
            // -----------------------------------------------------

            var payment = await _context.Payments

                .Include(x => x.Booking)
                .ThenInclude(x => x.PG)

                .Include(x => x.Booking)
                .ThenInclude(x => x.Room)

                .Include(x => x.Booking)
                .ThenInclude(x => x.Bed)

                .FirstOrDefaultAsync(x =>
                    x.PaymentId == id &&
                    x.UserId == userId.Value);

            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }


        // =========================================================
        // PAYMENT CALCULATION CLASS
        // =========================================================

        private class PaymentCalculation
        {
            public bool CanPay { get; set; }

            public string Message { get; set; } = string.Empty;

            public decimal Amount { get; set; }

            public string PaymentType { get; set; } = "Monthly";

            public DateTime? BillingMonth { get; set; }

            public DateTime? FromDate { get; set; }

            public DateTime? ToDate { get; set; }
        }
    }
}