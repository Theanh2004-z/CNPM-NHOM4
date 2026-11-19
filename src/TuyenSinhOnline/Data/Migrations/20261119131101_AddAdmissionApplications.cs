using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuyenSinhOnline.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmissionApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoundMajorMethodId = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<byte>(type: "tinyint", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    PhoneNumber = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SchoolName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GraduationYear = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdmissionApplications_ApplicationStatuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "ApplicationStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionApplications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionApplications_RoundMajorMethods_RoundMajorMethodId",
                        column: x => x.RoundMajorMethodId,
                        principalTable: "RoundMajorMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    FieldKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FieldLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationScores_AdmissionApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "AdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_ApplicationCode",
                table: "AdmissionApplications",
                column: "ApplicationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_RoundMajorMethodId",
                table: "AdmissionApplications",
                column: "RoundMajorMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_StatusId",
                table: "AdmissionApplications",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionApplications_UserId_RoundMajorMethodId",
                table: "AdmissionApplications",
                columns: new[] { "UserId", "RoundMajorMethodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationScores_ApplicationId_FieldKey",
                table: "ApplicationScores",
                columns: new[] { "ApplicationId", "FieldKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationScores");

            migrationBuilder.DropTable(
                name: "AdmissionApplications");
        }
    }
}
