using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.Models;

namespace PGManagement.Controllers
{
    public class BedController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BedController(ApplicationDbContext context)
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
        // BED INDEX
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Index(int? roomId)
        {
            if (!IsOwner(out int ownerId))
                return RedirectToAction("Login", "Account");


            // -----------------------------------------------------
            // Load owner's rooms for filter
            // -----------------------------------------------------

            var rooms = await _context.Rooms
                .Include(r => r.PG)
                .Where(r =>
                    r.PG != null &&
                    r.PG.OwnerId == ownerId)
                .OrderBy(r => r.PG!.PGName)
                .ThenBy(r => r.RoomNumber)
                .ToListAsync();


            ViewBag.Rooms = rooms;

            ViewBag.SelectedRoomId = roomId;


            // -----------------------------------------------------
            // Load beds belonging only to owner's PGs
            // -----------------------------------------------------

            var query = _context.Beds
                .Include(b => b.Room)
                    .ThenInclude(r => r!.PG)
                .Where(b =>
                    b.Room != null &&
                    b.Room.PG != null &&
                    b.Room.PG.OwnerId == ownerId);


            // -----------------------------------------------------
            // Filter by room
            // -----------------------------------------------------

            if (roomId.HasValue)
            {
                query = query.Where(
                    b => b.RoomId == roomId.Value);
            }


            var beds = await query
                .OrderBy(b => b.Room!.PG!.PGName)
                .ThenBy(b => b.Room!.RoomNumber)
                .ThenBy(b => b.BedNumber)
                .ToListAsync();


            // -----------------------------------------------------
            // Calculate actual room availability
            // -----------------------------------------------------

            foreach (var room in rooms)
            {
                var roomBeds = await _context.Beds
                    .Where(b => b.RoomId == room.RoomId)
                    .ToListAsync();

                room.AvailableBeds =
                    roomBeds.Count(
                        b => b.Status == "Available");

                room.Status =
                    room.AvailableBeds > 0
                        ? "Available"
                        : "Full";
            }


            return View(beds);
        }


        // =========================================================
        // CREATE BED - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Create(int roomId)
        {
            if (!IsOwner(out int ownerId))
                return RedirectToAction("Login", "Account");


            var room = await _context.Rooms
                .Include(r => r.PG)
                .Include(r => r.Beds)
                .FirstOrDefaultAsync(r =>
                    r.RoomId == roomId &&
                    r.PG != null &&
                    r.PG.OwnerId == ownerId);


            if (room == null)
                return NotFound();


            // -----------------------------------------------------
            // Capacity check
            // -----------------------------------------------------

            var existingBeds =
                room.Beds?.Count ?? 0;


            if (existingBeds >= room.Capacity)
            {
                TempData["Error"] =
                    $"Room {room.RoomNumber} has already reached " +
                    $"its maximum capacity of {room.Capacity} beds.";

                return RedirectToAction(
                    nameof(Index),
                    new { roomId });
            }


            ViewBag.Room = room;


            var model = new Bed
            {
                RoomId = roomId,
                Status = "Available"
            };


            return View(model);
        }


        // =========================================================
        // CREATE BED - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Bed model)
        {
            if (!IsOwner(out int ownerId))
                return RedirectToAction("Login", "Account");


            var room = await _context.Rooms
                .Include(r => r.PG)
                .Include(r => r.Beds)
                .FirstOrDefaultAsync(r =>
                    r.RoomId == model.RoomId &&
                    r.PG != null &&
                    r.PG.OwnerId == ownerId);


            if (room == null)
                return NotFound();


            // -----------------------------------------------------
            // Capacity
            // -----------------------------------------------------

            var existingBeds =
                room.Beds?.Count ?? 0;


            if (existingBeds >= room.Capacity)
            {
                ModelState.AddModelError(
                    "",
                    $"This room already has the maximum " +
                    $"capacity of {room.Capacity} beds.");
            }


            // -----------------------------------------------------
            // Bed number validation
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(model.BedNumber))
            {
                ModelState.AddModelError(
                    "BedNumber",
                    "Bed number is required.");
            }


            // -----------------------------------------------------
            // Duplicate bed number
            // -----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(model.BedNumber))
            {
                var duplicate =
                    await _context.Beds.AnyAsync(b =>
                        b.RoomId == model.RoomId &&
                        b.BedNumber == model.BedNumber);

                if (duplicate)
                {
                    ModelState.AddModelError(
                        "BedNumber",
                        "This bed number already exists in this room.");
                }
            }


            if (!ModelState.IsValid)
            {
                ViewBag.Room = room;

                return View(model);
            }


            // -----------------------------------------------------
            // New beds are always available
            // -----------------------------------------------------

            model.Status = "Available";

            model.Room = null;


            _context.Beds.Add(model);


            await _context.SaveChangesAsync();


            // -----------------------------------------------------
            // Synchronize room availability
            // -----------------------------------------------------

            await SyncRoomAvailability(room.RoomId);


            TempData["Success"] =
                $"Bed {model.BedNumber} added successfully.";


            return RedirectToAction(
                nameof(Index),
                new { roomId = model.RoomId });
        }


        // =========================================================
        // DELETE BED
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsOwner(out int ownerId))
                return RedirectToAction("Login", "Account");


            var bed = await _context.Beds
                .Include(b => b.Room)
                    .ThenInclude(r => r!.PG)
                .FirstOrDefaultAsync(b =>
                    b.BedId == id &&
                    b.Room != null &&
                    b.Room.PG != null &&
                    b.Room.PG.OwnerId == ownerId);


            if (bed == null)
                return NotFound();


            // -----------------------------------------------------
            // Occupied bed cannot be deleted
            // -----------------------------------------------------

            if (bed.Status == "Occupied")
            {
                TempData["Error"] =
                    $"Bed {bed.BedNumber} is currently occupied " +
                    "and cannot be deleted.";

                return RedirectToAction(
                    nameof(Index),
                    new { roomId = bed.RoomId });
            }


            // -----------------------------------------------------
            // Check booking history
            // -----------------------------------------------------

            var hasBookings =
                await _context.Bookings.AnyAsync(
                    b => b.BedId == bed.BedId);


            if (hasBookings)
            {
                TempData["Error"] =
                    $"Bed {bed.BedNumber} cannot be deleted because " +
                    "booking history exists for this bed.";

                return RedirectToAction(
                    nameof(Index),
                    new { roomId = bed.RoomId });
            }


            var roomId = bed.RoomId;

            var bedNumber = bed.BedNumber;


            _context.Beds.Remove(bed);


            await _context.SaveChangesAsync();


            // -----------------------------------------------------
            // Synchronize room
            // -----------------------------------------------------

            await SyncRoomAvailability(roomId);


            TempData["Success"] =
                $"Bed {bedNumber} deleted successfully.";


            return RedirectToAction(
                nameof(Index),
                new { roomId });
        }


        // =========================================================
        // SYNCHRONIZE ROOM AVAILABILITY
        // =========================================================

        private async Task SyncRoomAvailability(int roomId)
        {
            var room = await _context.Rooms
                .Include(r => r.Beds)
                .FirstOrDefaultAsync(
                    r => r.RoomId == roomId);


            if (room == null)
                return;


            var beds = room.Beds?.ToList()
                ?? new List<Bed>();


            room.AvailableBeds =
                beds.Count(
                    b => b.Status == "Available");


            room.Status =
                room.AvailableBeds > 0
                    ? "Available"
                    : "Full";


            await _context.SaveChangesAsync();
        }
    }
}