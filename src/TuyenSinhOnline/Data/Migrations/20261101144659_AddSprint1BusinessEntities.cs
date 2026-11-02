using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuyenSinhOnline.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSprint1BusinessEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmissionMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequirementsDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequiredFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AdmissionYear = table.Column<short>(type: "smallint", nullable: false),
                    StartAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionRounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationStatuses",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Majors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Majors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoundMajors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoundId = table.Column<int>(type: "int", nullable: false),
                    MajorId = table.Column<int>(type: "int", nullable: false),
                    Quota = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundMajors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundMajors_AdmissionRounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "AdmissionRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoundMajors_Majors_MajorId",
                        column: x => x.MajorId,
                        principalTable: "Majors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoundMajorMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoundMajorId = table.Column<int>(type: "int", nullable: false),
                    MethodId = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundMajorMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundMajorMethods_AdmissionMethods_MethodId",
                        column: x => x.MethodId,
                        principalTable: "AdmissionMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoundMajorMethods_RoundMajors_RoundMajorId",
                        column: x => x.RoundMajorId,
                        principalTable: "RoundMajors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionMethods_Code",
                table: "AdmissionMethods",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionRounds_Code",
                table: "AdmissionRounds",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatuses_Code",
                table: "ApplicationStatuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Majors_Code",
                table: "Majors",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoundMajorMethods_MethodId",
                table: "RoundMajorMethods",
                column: "MethodId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundMajorMethods_RoundMajorId_MethodId",
                table: "RoundMajorMethods",
                columns: new[] { "RoundMajorId", "MethodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoundMajors_MajorId",
                table: "RoundMajors",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundMajors_RoundId_MajorId",
                table: "RoundMajors",
                columns: new[] { "RoundId", "MajorId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationStatuses");

            migrationBuilder.DropTable(
                name: "RoundMajorMethods");

            migrationBuilder.DropTable(
                name: "AdmissionMethods");

            migrationBuilder.DropTable(
                name: "RoundMajors");

            migrationBuilder.DropTable(
                name: "AdmissionRounds");

            migrationBuilder.DropTable(
                name: "Majors");
        }
    }
}
