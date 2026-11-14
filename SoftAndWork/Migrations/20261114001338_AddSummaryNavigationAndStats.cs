using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftAndWork.Migrations
{
    /// <inheritdoc />
    public partial class AddSummaryNavigationAndStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionResponses_StudySessions_StudySessionId",
                table: "SessionResponses");

            migrationBuilder.AddColumn<int>(
                name: "MaterialId",
                table: "StudySessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlank",
                table: "SessionResponses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Materials",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionResponses_StudySessions_StudySessionId",
                table: "SessionResponses",
                column: "StudySessionId",
                principalTable: "StudySessions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionResponses_StudySessions_StudySessionId",
                table: "SessionResponses");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "IsBlank",
                table: "SessionResponses");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Materials");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionResponses_StudySessions_StudySessionId",
                table: "SessionResponses",
                column: "StudySessionId",
                principalTable: "StudySessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
