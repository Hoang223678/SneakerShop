using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SneakerShop.BuildingBlocks;

#nullable disable

namespace Inventory.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSneakerIdToStockItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SneakerId",
                table: "StockItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            foreach (var variant in SeedCatalog.Variants)
            {
                migrationBuilder.Sql(
                    $"UPDATE [dbo].[StockItems] SET [SneakerId] = '{variant.SneakerId}' WHERE [VariantId] = '{variant.Id}';");
            }

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_SneakerId",
                table: "StockItems",
                column: "SneakerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockItems_SneakerId",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "SneakerId",
                table: "StockItems");
        }
    }
}
