using Microsoft.AspNetCore.Identity;
using PGManagement.Models;

namespace PGManagement.Data
{
    public static class AdminSeeder
    {
        public static async Task SeedAdminAsync(
            ApplicationDbContext context)
        {
            // Check whether admin already exists
            var adminExists = context.Admins.Any();

            if (adminExists)
            {
                return;
            }

            var admin = new Admin
            {
                FullName = "System Administrator",
                Email = "admin@pgmanagement.com",
                CreatedDate = DateTime.Now,
                Status = "Active"
            };

            // Create password hash
            var passwordHasher = new PasswordHasher<Admin>();

            admin.PasswordHash =
                passwordHasher.HashPassword(
                    admin,
                    "Admin@123");

            context.Admins.Add(admin);

            await context.SaveChangesAsync();
        }
    }
}