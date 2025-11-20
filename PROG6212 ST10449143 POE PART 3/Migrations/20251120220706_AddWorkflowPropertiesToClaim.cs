using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROG6212_ST10449143_POE_PART_1.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowPropertiesToClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoordinatorApprover",
                table: "Claims",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CoordinatorReviewDate",
                table: "Claims",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurrentStage",
                table: "Claims",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "CoordinatorReview");

            migrationBuilder.AddColumn<bool>(
                name: "IsCoordinatorApproved",
                table: "Claims",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsManagerApproved",
                table: "Claims",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ManagerApprover",
                table: "Claims",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ManagerReviewDate",
                table: "Claims",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoordinatorApprover",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "CoordinatorReviewDate",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "CurrentStage",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "IsCoordinatorApproved",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "IsManagerApproved",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ManagerApprover",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ManagerReviewDate",
                table: "Claims");
        }
    }
}
