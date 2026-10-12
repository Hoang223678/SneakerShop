using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordering.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentExpiresAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [dbo].[Orders]
                SET [PaymentExpiresAt] = DATEADD(minute, 15, [CreatedAt])
                WHERE [PaymentExpiresAt] IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PaymentExpiresAt",
                table: "Orders",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentExpiresAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "Orders");
        }
    }
}
