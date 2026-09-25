using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KmcEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatNumbersAndBookingReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BookingReference",
                table: "Reservations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SeatNumbers",
                table: "Reservations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookingReference",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "SeatNumbers",
                table: "Reservations");
        }
    }
}
