using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuyenSinhOnline.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdmissionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "AspNetUsers");

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
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Admissio__3214EC075A67711C", x => x.Id);
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
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Admissio__3214EC071E0265CA", x => x.Id);
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
                    table.PrimaryKey("PK__Applicat__3214EC07D4936CAD", x => x.Id);
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
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Majors__3214EC0790908F8A", x => x.Id);
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
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__RoundMaj__3214EC074183A782", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundMajors_AdmissionRounds",
                        column: x => x.RoundId,
                        principalTable: "AdmissionRounds",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RoundMajors_Majors",
                        column: x => x.MajorId,
                        principalTable: "Majors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RoundMajorMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoundMajorId = table.Column<int>(type: "int", nullable: false),
                    MethodId = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__RoundMaj__3214EC07C92E492B", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundMajorMethods_AdmissionMethods",
                        column: x => x.MethodId,
                        principalTable: "AdmissionMethods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RoundMajorMethods_RoundMajors",
                        column: x => x.RoundMajorId,
                        principalTable: "RoundMajors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "EmailIndex1",
                table: "AspNetUsers",
                column: "NormalizedEmail",
                unique: true,
                filter: "([NormalizedEmail] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex1",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "([NormalizedUserName] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UQ_AdmissionMethods_Code",
                table: "AdmissionMethods",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_AdmissionRounds_Code",
                table: "AdmissionRounds",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ApplicationStatuses_Code",
                table: "ApplicationStatuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Majors_Code",
                table: "Majors",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoundMajorMethods_MethodId",
                table: "RoundMajorMethods",
                column: "MethodId");

            migrationBuilder.CreateIndex(
                name: "UQ_RoundMajorMethods",
                table: "RoundMajorMethods",
                columns: new[] { "RoundMajorId", "MethodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoundMajors_MajorId",
                table: "RoundMajors",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "UQ_RoundMajors_Round_Major",
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

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "EmailIndex1",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "UserNameIndex1",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail",
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");
        }
    }
}
