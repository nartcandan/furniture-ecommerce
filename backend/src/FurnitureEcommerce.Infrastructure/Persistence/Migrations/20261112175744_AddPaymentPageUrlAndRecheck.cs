using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurnitureEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentPageUrlAndRecheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentPageUrl",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentRecheckAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentRecheckAt",
                table: "Orders",
                column: "PaymentRecheckAt",
                filter: "\"PaymentRecheckAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentRecheckAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentPageUrl",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentRecheckAt",
                table: "Orders");
        }
    }
}
