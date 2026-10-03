using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.Models;

namespace PGManagement.Controllers
{
    public class RoomController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoomController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // OWNER AUTHENTICATION
        // =========================================================

        private bool IsOwner(out int ownerId)
        {
            var sessionOwnerId = HttpContext.Session.GetInt32("OwnerId");
            var userType = HttpContext.Session.GetString("UserType");

            if (sessionOwnerId == null || userType != "OWNER")
            {
                ownerId = 0;
                return false;
            }

            ownerId = sessionOwnerId.Value;
            return true;
        }

        // =========================================================
        // ROOM LIST
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Index(int? pgId)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var query = _context.Rooms
                .Include(x => x.PG)
                .Include(x => x.Beds)
                .Where(x =>
                    x.PG != null &&
                    x.PG.OwnerId == ownerId);

            if (pgId.HasValue)
            {
                query = query.Where(x => x.PGId == pgId.Value);
            }

            var rooms = await query
                .OrderBy(x => x.PGId)
                .ThenBy(x => x.RoomNumber)
                .ToListAsync();

            // Make sure displayed availability reflects actual beds
            foreach (var room in rooms)
            {
                room.AvailableBeds = room.Beds.Count(
                    x => x.Status == "Available");

                room.Status = room.AvailableBeds > 0
                    ? "Available"
                    : "Full";
            }

            ViewBag.PGId = pgId;

            return View(rooms);
        }

        // =========================================================
        // CREATE ROOM - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Create(int? pgId)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var pgs = await _context.PGs
                .Where(x => x.OwnerId == ownerId)
                .OrderBy(x => x.PGName)
                .ToListAsync();

            if (!pgs.Any())
            {
                TempData["Error"] =
                    "Please create a PG before adding a room.";

                return RedirectToAction("MyPGs", "PG");
            }

            ViewBag.PGs = pgs;

            var room = new Room
            {
                Capacity = 1,
                AvailableBeds = 1,
                MonthlyRent = 0,
                DailyRent = 0,
                Status = "Available"
            };

            if (pgId.HasValue)
            {
                var pgExists = pgs.Any(
                    x => x.PGId == pgId.Value);

                if (!pgExists)
                {
                    return NotFound();
                }

                room.PGId = pgId.Value;
            }

            return View(room);
        }

        // =========================================================
        // CREATE ROOM - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Room model)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            // =====================================================
            // VALIDATE PG
            // =====================================================

            var pg = await _context.PGs
                .FirstOrDefaultAsync(x =>
                    x.PGId == model.PGId &&
                    x.OwnerId == ownerId);

            if (pg == null)
            {
                ModelState.AddModelError(
                    "PGId",
                    "Invalid PG selected.");
            }

            // =====================================================
            // VALIDATE ROOM NUMBER
            // =====================================================

            if (string.IsNullOrWhiteSpace(model.RoomNumber))
            {
                ModelState.AddModelError(
                    "RoomNumber",
                    "Room number is required.");
            }

            // =====================================================
            // VALIDATE ROOM TYPE
            // =====================================================

            if (model.RoomType != "A/C" &&
                model.RoomType != "Non-A/C")
            {
                ModelState.AddModelError(
                    "RoomType",
                    "Please select A/C or Non-A/C.");
            }

            // =====================================================
            // VALIDATE SHARING TYPE
            // =====================================================

            var validSharingTypes = new[]
            {
                "Single",
                "Double",
                "Triple",
                "Four"
            };

            if (!validSharingTypes.Contains(model.SharingType))
            {
                ModelState.AddModelError(
                    "SharingType",
                    "Please select a valid sharing type.");
            }

            // =====================================================
            // VALIDATE CAPACITY
            // =====================================================

            if (model.Capacity <= 0)
            {
                ModelState.AddModelError(
                    "Capacity",
                    "Capacity must be greater than 0.");
            }

            // =====================================================
            // SHARING TYPE / CAPACITY CHECK
            // =====================================================

            var expectedCapacity = model.SharingType switch
            {
                "Single" => 1,
                "Double" => 2,
                "Triple" => 3,
                "Four" => 4,
                _ => 0
            };

            if (expectedCapacity > 0 &&
                model.Capacity != expectedCapacity)
            {
                ModelState.AddModelError(
                    "Capacity",
                    $"{model.SharingType} sharing must have capacity {expectedCapacity}.");
            }

            // =====================================================
            // VALIDATE MONTHLY RENT
            // =====================================================

            if (model.MonthlyRent <= 0)
            {
                ModelState.AddModelError(
                    "MonthlyRent",
                    "Monthly rent must be greater than 0.");
            }

            // =====================================================
            // VALIDATE DAILY RENT
            // =====================================================

            if (model.DailyRent <= 0)
            {
                ModelState.AddModelError(
                    "DailyRent",
                    "Daily rent must be greater than 0.");
            }

            // =====================================================
            // RETURN IF VALIDATION FAILED
            // =====================================================

            if (!ModelState.IsValid)
            {
                await LoadPGs(ownerId);
                return View(model);
            }

            // =====================================================
            // DUPLICATE ROOM NUMBER
            // =====================================================

            var duplicateRoom = await _context.Rooms
                .AnyAsync(x =>
                    x.PGId == model.PGId &&
                    x.RoomNumber == model.RoomNumber);

            if (duplicateRoom)
            {
                ModelState.AddModelError(
                    "RoomNumber",
                    "This room number already exists in this PG.");

                await LoadPGs(ownerId);

                return View(model);
            }

            // =====================================================
            // CREATE ROOM
            // =====================================================

            model.AvailableBeds = model.Capacity;

            model.Status = "Available";

            model.PG = null;

            _context.Rooms.Add(model);

            await _context.SaveChangesAsync();

            // =====================================================
            // AUTOMATICALLY CREATE BEDS
            // =====================================================

            var beds = new List<Bed>();

            for (int i = 1; i <= model.Capacity; i++)
            {
                beds.Add(new Bed
                {
                    BedNumber = $"{model.RoomNumber}-B{i}",
                    RoomId = model.RoomId,
                    Status = "Available"
                });
            }

            _context.Beds.AddRange(beds);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Room {model.RoomNumber} added successfully with {model.Capacity} beds.";

            return RedirectToAction(
                nameof(Index),
                new { pgId = model.PGId });
        }
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!IsOwner(out int ownerId))
                return RedirectToAction("Login", "Account");

            var room = await _context.Rooms
                .Include(x => x.PG)
                .Include(x => x.Beds)
                .FirstOrDefaultAsync(x =>
                    x.RoomId == id &&
                    x.PG != null &&
                    x.PG.OwnerId == ownerId);

            if (room == null)
                return NotFound();

            room.AvailableBeds =
                room.Beds.Count(x => x.Status == "Available");

            room.Status =
                room.AvailableBeds > 0
                    ? "Available"
                    : "Full";

            return View(room);
        }

        // =========================================================
        // EDIT ROOM - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var room = await _context.Rooms
                .Include(x => x.PG)
                .Include(x => x.Beds)
                .FirstOrDefaultAsync(x =>
                    x.RoomId == id &&
                    x.PG != null &&
                    x.PG.OwnerId == ownerId);

            if (room == null)
            {
                return NotFound();
            }

            var pgs = await _context.PGs
                .Where(x => x.OwnerId == ownerId)
                .OrderBy(x => x.PGName)
                .ToListAsync();

            ViewBag.PGs = pgs;

            // Always calculate actual availability
            room.AvailableBeds = room.Beds.Count(
                x => x.Status == "Available");

            return View(room);
        }

        // =========================================================
        // EDIT ROOM - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Room model)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (id != model.RoomId)
            {
                return NotFound();
            }

            // =====================================================
            // FIND EXISTING ROOM
            // =====================================================

            var room = await _context.Rooms
                .Include(x => x.PG)
                .Include(x => x.Beds)
                .FirstOrDefaultAsync(x =>
                    x.RoomId == id &&
                    x.PG != null &&
                    x.PG.OwnerId == ownerId);

            if (room == null)
            {
                return NotFound();
            }

            // =====================================================
            // VALIDATE PG
            // =====================================================

            var pgExists = await _context.PGs
                .AnyAsync(x =>
                    x.PGId == model.PGId &&
                    x.OwnerId == ownerId);

            if (!pgExists)
            {
                ModelState.AddModelError(
                    "PGId",
                    "Invalid PG selected.");
            }

            // =====================================================
            // VALIDATE ROOM NUMBER
            // =====================================================

            if (string.IsNullOrWhiteSpace(model.RoomNumber))
            {
                ModelState.AddModelError(
                    "RoomNumber",
                    "Room number is required.");
            }

            // =====================================================
            // VALIDATE ROOM TYPE
            // =====================================================

            if (model.RoomType != "A/C" &&
                model.RoomType != "Non-A/C")
            {
                ModelState.AddModelError(
                    "RoomType",
                    "Please select A/C or Non-A/C.");
            }

            // =====================================================
            // VALIDATE SHARING TYPE
            // =====================================================

            var validSharingTypes = new[]
            {
                "Single",
                "Double",
                "Triple",
                "Four"
            };

            if (!validSharingTypes.Contains(model.SharingType))
            {
                ModelState.AddModelError(
                    "SharingType",
                    "Please select a valid sharing type.");
            }

            // =====================================================
            // EXPECTED CAPACITY
            // =====================================================

            var expectedCapacity = model.SharingType switch
            {
                "Single" => 1,
                "Double" => 2,
                "Triple" => 3,
                "Four" => 4,
                _ => 0
            };

            if (model.Capacity <= 0)
            {
                ModelState.AddModelError(
                    "Capacity",
                    "Capacity must be greater than 0.");
            }

            if (expectedCapacity > 0 &&
                model.Capacity != expectedCapacity)
            {
                ModelState.AddModelError(
                    "Capacity",
                    $"{model.SharingType} sharing must have capacity {expectedCapacity}.");
            }

            // =====================================================
            // VALIDATE MONTHLY RENT
            // =====================================================

            if (model.MonthlyRent <= 0)
            {
                ModelState.AddModelError(
                    "MonthlyRent",
                    "Monthly rent must be greater than 0.");
            }

            // =====================================================
            // VALIDATE DAILY RENT
            // =====================================================

            if (model.DailyRent <= 0)
            {
                ModelState.AddModelError(
                    "DailyRent",
                    "Daily rent must be greater than 0.");
            }

            // =====================================================
            // CURRENT OCCUPIED BEDS
            // =====================================================

            var occupiedBeds = room.Beds.Count(
                x => x.Status == "Occupied");

            // Cannot reduce capacity below occupied beds
            if (model.Capacity < occupiedBeds)
            {
                ModelState.AddModelError(
                    "Capacity",
                    $"Capacity cannot be less than the {occupiedBeds} occupied bed(s).");
            }

            // =====================================================
            // RETURN IF INVALID
            // =====================================================

            if (!ModelState.IsValid)
            {
                await LoadPGs(ownerId);

                model.Beds = room.Beds;

                return View(model);
            }

            // =====================================================
            // CHECK DUPLICATE ROOM NUMBER
            // =====================================================

            var duplicateRoom = await _context.Rooms
                .AnyAsync(x =>
                    x.RoomId != id &&
                    x.PGId == model.PGId &&
                    x.RoomNumber == model.RoomNumber);

            if (duplicateRoom)
            {
                ModelState.AddModelError(
                    "RoomNumber",
                    "This room number already exists in this PG.");

                await LoadPGs(ownerId);

                model.Beds = room.Beds;

                return View(model);
            }

            // =====================================================
            // UPDATE ROOM
            // =====================================================

            room.RoomNumber = model.RoomNumber;
            room.RoomType = model.RoomType;
            room.SharingType = model.SharingType;
            room.MonthlyRent = model.MonthlyRent;
            room.DailyRent = model.DailyRent;
            room.PGId = model.PGId;

            // =====================================================
            // BED CAPACITY MANAGEMENT
            // =====================================================

            var currentCapacity = room.Beds.Count;

            // -----------------------------------------------------
            // INCREASE CAPACITY
            // -----------------------------------------------------

            if (model.Capacity > currentCapacity)
            {
                var bedsToAdd =
                    model.Capacity - currentCapacity;

                for (int i = currentCapacity + 1;
                     i <= model.Capacity;
                     i++)
                {
                    room.Beds.Add(new Bed
                    {
                        BedNumber =
                            $"{model.RoomNumber}-B{i}",

                        Status = "Available"
                    });
                }
            }

            // -----------------------------------------------------
            // DECREASE CAPACITY
            // -----------------------------------------------------

            else if (model.Capacity < currentCapacity)
            {
                var bedsToRemove =
                    currentCapacity - model.Capacity;

                var removableBeds = room.Beds
                    .Where(x => x.Status == "Available")
                    .OrderByDescending(x => x.BedId)
                    .Take(bedsToRemove)
                    .ToList();

                if (removableBeds.Count < bedsToRemove)
                {
                    ModelState.AddModelError(
                        "Capacity",
                        "Some beds are occupied and cannot be removed.");

                    await LoadPGs(ownerId);

                    model.Beds = room.Beds;

                    return View(model);
                }

                _context.Beds.RemoveRange(removableBeds);
            }

            // =====================================================
            // RENAME BED NUMBERS IF ROOM NUMBER CHANGED
            // =====================================================

            var remainingBeds = room.Beds
                .Where(x =>
                    _context.Entry(x).State !=
                    EntityState.Deleted)
                .OrderBy(x => x.BedId)
                .ToList();

            for (int i = 0; i < remainingBeds.Count; i++)
            {
                remainingBeds[i].BedNumber =
                    $"{model.RoomNumber}-B{i + 1}";
            }

            // =====================================================
            // CALCULATE AVAILABLE BEDS
            // =====================================================

            var deletedBedIds = room.Beds
                .Where(x =>
                    _context.Entry(x).State ==
                    EntityState.Deleted)
                .Select(x => x.BedId)
                .ToHashSet();

            var activeBeds = room.Beds
                .Where(x => !deletedBedIds.Contains(x.BedId))
                .ToList();

            room.AvailableBeds = activeBeds.Count(
                x => x.Status == "Available");

            room.Status = room.AvailableBeds > 0
                ? "Available"
                : "Full";

            // =====================================================
            // SAVE
            // =====================================================

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Room updated successfully.";

            return RedirectToAction(
                nameof(Index),
                new { pgId = room.PGId });
        }

        // =========================================================
        // DELETE ROOM
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            // =====================================================
            // FIND ROOM
            // =====================================================

            var room = await _context.Rooms
                .Include(x => x.PG)
                .Include(x => x.Beds)
                .FirstOrDefaultAsync(x =>
                    x.RoomId == id &&
                    x.PG != null &&
                    x.PG.OwnerId == ownerId);

            if (room == null)
            {
                return NotFound();
            }

            // =====================================================
            // CHECK OCCUPIED BEDS
            // =====================================================

            var occupiedBeds = room.Beds
                .Any(x => x.Status == "Occupied");

            if (occupiedBeds)
            {
                TempData["Error"] =
                    "This room cannot be deleted because one or more beds are occupied.";

                return RedirectToAction(
                    nameof(Index),
                    new { pgId = room.PGId });
            }

            // =====================================================
            // CHECK BOOKINGS
            // =====================================================

            var hasBookings = await _context.Bookings
                .AnyAsync(x => x.RoomId == room.RoomId);

            if (hasBookings)
            {
                TempData["Error"] =
                    "This room cannot be deleted because booking records exist.";

                return RedirectToAction(
                    nameof(Index),
                    new { pgId = room.PGId });
            }

            // =====================================================
            // DELETE BEDS
            // =====================================================

            if (room.Beds.Any())
            {
                _context.Beds.RemoveRange(room.Beds);
            }

            // =====================================================
            // DELETE ROOM
            // =====================================================

            var pgId = room.PGId;

            _context.Rooms.Remove(room);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Room {room.RoomNumber} deleted successfully.";

            return RedirectToAction(
                nameof(Index),
                new { pgId });
        }

        // =========================================================
        // LOAD PG LIST
        // =========================================================

        private async Task LoadPGs(int ownerId)
        {
            ViewBag.PGs = await _context.PGs
                .Where(x => x.OwnerId == ownerId)
                .OrderBy(x => x.PGName)
                .ToListAsync();
        }
    }
}