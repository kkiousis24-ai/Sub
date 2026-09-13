using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubscriptionName",
                table: "Subscriptions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionName",
                table: "Subscriptions");
        }
    }
}
