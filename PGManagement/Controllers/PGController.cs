using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PGManagement.Data;
using PGManagement.Models;

namespace PGManagement.Controllers
{
    public class PGController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PGController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // =========================================================
        // AUTHENTICATION CHECK
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
        // GET: PG/MyPGs
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> MyPGs()
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var pgs = await _context.PGs
                .Where(x => x.OwnerId == ownerId)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            return View(pgs);
        }

        // =========================================================
        // GET: PG/Create
        // =========================================================

        [HttpGet]
        public IActionResult Create()
        {
            if (!IsOwner(out _))
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        // =========================================================
        // POST: PG/Create
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            PG model,
            IFormFile? imageFile)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            // These values are controlled by the server
            ModelState.Remove(nameof(PG.ImagePath));
            ModelState.Remove(nameof(PG.Owner));
            ModelState.Remove(nameof(PG.Rooms));

            // =====================================================
            // IMAGE VALIDATION
            // =====================================================

            if (imageFile != null && imageFile.Length > 0)
            {
                ValidateImage(imageFile);

                if (!ModelState.IsValid)
                {
                    return View(model);
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // =====================================================
            // PG DETAILS
            // =====================================================

            model.OwnerId = ownerId;

            // Every newly created PG requires admin approval
            model.Status = "Pending";

            model.CreatedDate = DateTime.Now;

            // =====================================================
            // SAVE IMAGE
            // =====================================================

            if (imageFile != null && imageFile.Length > 0)
            {
                model.ImagePath = await SaveImageAsync(imageFile);
            }

            // =====================================================
            // SAVE PG
            // =====================================================

            _context.PGs.Add(model);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "PG added successfully. Waiting for admin approval.";

            return RedirectToAction(nameof(MyPGs));
        }

        // =========================================================
        // GET: PG/Details/5
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var pg = await _context.PGs
                .Include(x => x.Rooms)
                    .ThenInclude(x => x.Beds)
                .FirstOrDefaultAsync(x =>
                    x.PGId == id &&
                    x.OwnerId == ownerId);

            if (pg == null)
            {
                return NotFound();
            }

            return View(pg);
        }

        // =========================================================
        // GET: PG/Edit/5
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            var pg = await _context.PGs
                .FirstOrDefaultAsync(x =>
                    x.PGId == id &&
                    x.OwnerId == ownerId);

            if (pg == null)
            {
                return NotFound();
            }

            return View(pg);
        }

        // =========================================================
        // POST: PG/Edit/5
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            PG model,
            IFormFile? imageFile)
        {
            if (!IsOwner(out int ownerId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (id != model.PGId)
            {
                return NotFound();
            }

            // These properties should not be validated from the form
            ModelState.Remove(nameof(PG.ImagePath));
            ModelState.Remove(nameof(PG.Owner));
            ModelState.Remove(nameof(PG.Rooms));

            // =====================================================
            // FIND EXISTING PG
            // =====================================================

            var existingPG = await _context.PGs
                .FirstOrDefaultAsync(x =>
                    x.PGId == id &&
                    x.OwnerId == ownerId);

            if (existingPG == null)
            {
                return NotFound();
            }

            // =====================================================
            // IMAGE VALIDATION
            // =====================================================

            if (imageFile != null && imageFile.Length > 0)
            {
                ValidateImage(imageFile);

                if (!ModelState.IsValid)
                {
                    model.ImagePath = existingPG.ImagePath;
                    return View(model);
                }
            }

            if (!ModelState.IsValid)
            {
                model.ImagePath = existingPG.ImagePath;
                return View(model);
            }

            // =====================================================
            // SAVE OLD IMAGE PATH
            // =====================================================

            var oldImagePath = existingPG.ImagePath;

            // =====================================================
            // UPDATE PG INFORMATION
            // =====================================================

            existingPG.PGName = model.PGName;
            existingPG.Address = model.Address;
            existingPG.City = model.City;
            existingPG.Description = model.Description;
            existingPG.GenderType = model.GenderType;

            // =====================================================
            // IMAGE UPDATE
            // =====================================================

            if (imageFile != null && imageFile.Length > 0)
            {
                var newImagePath = await SaveImageAsync(imageFile);

                existingPG.ImagePath = newImagePath;

                // Delete old image after new image is saved
                DeleteImage(oldImagePath);
            }

            // =====================================================
            // SAVE CHANGES
            // =====================================================

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "PG details updated successfully.";

            return RedirectToAction(
                nameof(Details),
                new { id = existingPG.PGId });
        }

        // =========================================================
        // POST: PG/Delete/5
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
            // FIND PG
            // =====================================================

            var pg = await _context.PGs
                .FirstOrDefaultAsync(x =>
                    x.PGId == id &&
                    x.OwnerId == ownerId);

            if (pg == null)
            {
                TempData["Error"] = "PG not found.";

                return RedirectToAction(nameof(MyPGs));
            }

            // =====================================================
            // CHECK ROOMS
            // =====================================================

            var hasRooms = await _context.Rooms
                .AnyAsync(x => x.PGId == pg.PGId);

            if (hasRooms)
            {
                TempData["Error"] =
                    "This PG cannot be deleted because it has rooms. Delete the rooms first.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = pg.PGId });
            }

            // =====================================================
            // CHECK BOOKINGS
            // =====================================================

            var hasBookings = await _context.Bookings
                .AnyAsync(x => x.PGId == pg.PGId);

            if (hasBookings)
            {
                TempData["Error"] =
                    "This PG cannot be deleted because booking records exist.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = pg.PGId });
            }

            // =====================================================
            // DELETE IMAGE
            // =====================================================

            DeleteImage(pg.ImagePath);

            // =====================================================
            // DELETE PG
            // =====================================================

            _context.PGs.Remove(pg);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "PG deleted successfully.";

            return RedirectToAction(nameof(MyPGs));
        }

        // =========================================================
        // IMAGE VALIDATION
        // =========================================================

        private void ValidateImage(IFormFile imageFile)
        {
            // Maximum 5 MB
            if (imageFile.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(
                    "ImagePath",
                    "Image size must be less than 5 MB.");

                return;
            }

            var allowedExtensions = new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

            var extension = Path
                .GetExtension(imageFile.FileName)
                .ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "ImagePath",
                    "Only JPG, JPEG, PNG and WEBP images are allowed.");
            }
        }

        // =========================================================
        // SAVE IMAGE
        // =========================================================

        private async Task<string> SaveImageAsync(IFormFile imageFile)
        {
            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "pgs");

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var extension = Path
                .GetExtension(imageFile.FileName)
                .ToLowerInvariant();

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var filePath = Path.Combine(
                uploadFolder,
                fileName);

            await using var stream =
                new FileStream(
                    filePath,
                    FileMode.Create);

            await imageFile.CopyToAsync(stream);

            return $"/uploads/pgs/{fileName}";
        }

        // =========================================================
        // DELETE IMAGE
        // =========================================================

        private void DeleteImage(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            var relativePath = imagePath
                .TrimStart('/')
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar);

            var filePath = Path.Combine(
                _environment.WebRootPath,
                relativePath);

            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                }
                catch
                {
                    // Do not stop database operation
                    // if image deletion fails.
                }
            }
        }
    }
}