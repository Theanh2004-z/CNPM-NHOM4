using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuyenSinhOnline.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationStatusWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    FromStatusId = table.Column<byte>(type: "tinyint", nullable: false),
                    ToStatusId = table.Column<byte>(type: "tinyint", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationStatusHistories_AdmissionApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "AdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationStatusHistories_ApplicationStatuses_FromStatusId",
                        column: x => x.FromStatusId,
                        principalTable: "ApplicationStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationStatusHistories_ApplicationStatuses_ToStatusId",
                        column: x => x.ToStatusId,
                        principalTable: "ApplicationStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationStatusHistories_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationStatusTransitions",
                columns: table => new
                {
                    FromStatusId = table.Column<byte>(type: "tinyint", nullable: false),
                    ToStatusId = table.Column<byte>(type: "tinyint", nullable: false),
                    RequiresNote = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationStatusTransitions", x => new { x.FromStatusId, x.ToStatusId });
                    table.ForeignKey(
                        name: "FK_ApplicationStatusTransitions_ApplicationStatuses_FromStatusId",
                        column: x => x.FromStatusId,
                        principalTable: "ApplicationStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApplicationStatusTransitions_ApplicationStatuses_ToStatusId",
                        column: x => x.ToStatusId,
                        principalTable: "ApplicationStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistories_ApplicationId_ChangedAt",
                table: "ApplicationStatusHistories",
                columns: new[] { "ApplicationId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistories_ChangedByUserId",
                table: "ApplicationStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistories_FromStatusId",
                table: "ApplicationStatusHistories",
                column: "FromStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistories_ToStatusId",
                table: "ApplicationStatusHistories",
                column: "ToStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusTransitions_ToStatusId",
                table: "ApplicationStatusTransitions",
                column: "ToStatusId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationStatusHistories");

            migrationBuilder.DropTable(
                name: "ApplicationStatusTransitions");
        }
    }
}
