using Microsoft.EntityFrameworkCore;
using PGManagement.Models;

namespace PGManagement.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<Owner> Owners { get; set; }

        public DbSet<Admin> Admins { get; set; }

        public DbSet<PG> PGs { get; set; }

        public DbSet<Room> Rooms { get; set; }

        public DbSet<Bed> Beds { get; set; }

        public DbSet<Booking> Bookings { get; set; }

        public DbSet<Payment> Payments { get; set; }

        public DbSet<FoodMenu> FoodMenus { get; set; }


        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            // =============================================
            // BOOKING -> PAYMENT
            //
            // One Booking = MANY Payments
            //
            // Example:
            //
            // Booking #1
            //      |
            //      |--- Payment #1 (September)
            //      |--- Payment #2 (October)
            //      |--- Payment #3 (November)
            //
            // =============================================

            modelBuilder.Entity<Booking>()
                .HasMany(b => b.Payments)
                .WithOne(p => p.Booking)
                .HasForeignKey(p => p.BookingId)
                .OnDelete(DeleteBehavior.Cascade);


            // =============================================
            // BOOKING -> USER
            // =============================================

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);


            // =============================================
            // BOOKING -> PG
            // =============================================

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.PG)
                .WithMany()
                .HasForeignKey(b => b.PGId)
                .OnDelete(DeleteBehavior.Restrict);


            // =============================================
            // BOOKING -> ROOM
            // =============================================

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Room)
                .WithMany()
                .HasForeignKey(b => b.RoomId)
                .OnDelete(DeleteBehavior.Restrict);


            // =============================================
            // BOOKING -> BED
            // =============================================

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Bed)
                .WithMany()
                .HasForeignKey(b => b.BedId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}