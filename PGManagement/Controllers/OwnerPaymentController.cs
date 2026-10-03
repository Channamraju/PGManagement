using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.Models;

namespace PGManagement.Controllers
{
    public class OwnerPaymentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OwnerPaymentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ---------------------------------------------------------
        // OWNER LOGIN CHECK
        // ---------------------------------------------------------
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


        // ---------------------------------------------------------
        // PAYMENT LIST
        // GET: OwnerPayment/Index
        // ---------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var payments = await _context.Payments
                .AsNoTracking()

                .Include(p => p.User)

                .Include(p => p.Booking)
                    .ThenInclude(b => b!.PG)

                .Include(p => p.Booking)
                    .ThenInclude(b => b!.Room)

                .Include(p => p.Booking)
                    .ThenInclude(b => b!.Bed)

                .Where(p =>
                    p.Booking != null &&
                    p.Booking.PG != null &&
                    p.Booking.PG.OwnerId == ownerId)

                .OrderByDescending(p => p.PaymentDate)

                .ToListAsync();


            // -----------------------------------------------------
            // DASHBOARD STATISTICS
            // -----------------------------------------------------

            ViewBag.TotalPayments =
                payments.Count;

            ViewBag.SuccessfulPayments =
                payments.Count(p => p.Status == "Success");

            ViewBag.FailedPayments =
                payments.Count(p =>
                    p.Status != "Success");

            ViewBag.TotalAmount =
                payments
                    .Where(p => p.Status == "Success")
                    .Sum(p => p.Amount);


            // -----------------------------------------------------
            // MONTHLY PAYMENTS
            // -----------------------------------------------------

            ViewBag.MonthlyPayments =
                payments
                    .Where(p =>
                        p.Status == "Success" &&
                        p.PaymentType == "Monthly")
                    .Sum(p => p.Amount);


            // -----------------------------------------------------
            // DAILY PAYMENTS
            // -----------------------------------------------------

            ViewBag.DailyPayments =
                payments
                    .Where(p =>
                        p.Status == "Success" &&
                        p.PaymentType == "Daily")
                    .Sum(p => p.Amount);


            // -----------------------------------------------------
            // TODAY'S COLLECTION
            // -----------------------------------------------------

            ViewBag.TodayPayments =
                payments
                    .Where(p =>
                        p.Status == "Success" &&
                        p.PaymentDate.Date == DateTime.Today)
                    .Sum(p => p.Amount);


            // -----------------------------------------------------
            // CURRENT MONTH COLLECTION
            // -----------------------------------------------------

            ViewBag.CurrentMonthPayments =
                payments
                    .Where(p =>
                        p.Status == "Success" &&
                        p.PaymentDate.Year == DateTime.Today.Year &&
                        p.PaymentDate.Month == DateTime.Today.Month)
                    .Sum(p => p.Amount);


            return View(payments);
        }


        // ---------------------------------------------------------
        // PAYMENT DETAILS
        // GET: OwnerPayment/Details/5
        // ---------------------------------------------------------
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

            var payment = await _context.Payments

                .AsNoTracking()

                .Include(p => p.User)

                .Include(p => p.Booking)
                    .ThenInclude(b => b!.PG)

                .Include(p => p.Booking)
                    .ThenInclude(b => b!.Room)

                .Include(p => p.Booking)
                    .ThenInclude(b => b!.Bed)

                .FirstOrDefaultAsync(p =>
                    p.PaymentId == id &&
                    p.Booking != null &&
                    p.Booking.PG != null &&
                    p.Booking.PG.OwnerId == ownerId);

            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }
    }
}