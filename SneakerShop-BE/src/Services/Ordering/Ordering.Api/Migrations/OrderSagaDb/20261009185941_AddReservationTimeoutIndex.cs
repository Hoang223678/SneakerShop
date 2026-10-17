using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordering.Api.Migrations.OrderSagaDb
{
    /// <inheritdoc />
    public partial class AddReservationTimeoutIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OrderState_CurrentState_ReservationDeadline",
                table: "OrderState",
                columns: new[] { "CurrentState", "ReservationDeadline" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderState_CurrentState_ReservationDeadline",
                table: "OrderState");
        }
    }
}
