using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PGManagement.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBookingPaymentSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =====================================================
            // PAYMENT RELATIONSHIP
            // Change Booking -> Payment from ONE-TO-ONE
            // to ONE-TO-MANY
            // =====================================================

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_BookingId",
                table: "Payments");


            // =====================================================
            // ROOM
            // =====================================================

            migrationBuilder.AddColumn<decimal>(
                name: "DailyRent",
                table: "Rooms",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);


            // =====================================================
            // PAYMENT
            // =====================================================

            migrationBuilder.AddColumn<DateTime>(
                name: "BillingMonth",
                table: "Payments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FromDate",
                table: "Payments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentType",
                table: "Payments",
                type: "longtext",
                nullable: false,
                defaultValue: "Monthly")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ToDate",
                table: "Payments",
                type: "datetime(6)",
                nullable: true);


            // =====================================================
            // BOOKING
            // =====================================================

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Bookings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Bookings",
                type: "datetime(6)",
                nullable: false,
                defaultValue: DateTime.Now);

            migrationBuilder.AddColumn<string>(
                name: "StayType",
                table: "Bookings",
                type: "longtext",
                nullable: false,
                defaultValue: "Monthly")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Bookings",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);


            // =====================================================
            // CREATE NORMAL INDEX
            // NOT UNIQUE
            //
            // This allows:
            // Booking 1 -> Payment 1
            // Booking 1 -> Payment 2
            // Booking 1 -> Payment 3
            // =====================================================

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BookingId",
                table: "Payments",
                column: "BookingId");


            // =====================================================
            // RECREATE FOREIGN KEY
            // ONE BOOKING -> MANY PAYMENTS
            // =====================================================

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "BookingId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove foreign key first
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments");

            // Remove normal index
            migrationBuilder.DropIndex(
                name: "IX_Payments_BookingId",
                table: "Payments");


            // =====================================================
            // REMOVE ROOM FIELD
            // =====================================================

            migrationBuilder.DropColumn(
                name: "DailyRent",
                table: "Rooms");


            // =====================================================
            // REMOVE PAYMENT FIELDS
            // =====================================================

            migrationBuilder.DropColumn(
                name: "BillingMonth",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FromDate",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ToDate",
                table: "Payments");


            // =====================================================
            // REMOVE BOOKING FIELDS
            // =====================================================

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "StayType",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Bookings");


            // =====================================================
            // RESTORE OLD ONE-TO-ONE RELATIONSHIP
            // =====================================================

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BookingId",
                table: "Payments",
                column: "BookingId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "BookingId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}