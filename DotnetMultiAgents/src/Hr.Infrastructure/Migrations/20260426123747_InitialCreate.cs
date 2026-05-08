using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HiringOrganizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartmentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgencyDescription = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiringOrganizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Duties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Qualifications = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    OccupationalSeries = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayGradeMin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayGradeMax = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AppointmentType = table.Column<int>(type: "int", nullable: false),
                    WorkSchedule = table.Column<int>(type: "int", nullable: false),
                    OpenDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CloseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WhoMayApply = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DutyLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeleworkEligible = table.Column<bool>(type: "bit", nullable: false),
                    TravelRequired = table.Column<int>(type: "int", nullable: false),
                    SecurityClearance = table.Column<int>(type: "int", nullable: false),
                    SupervisoryStatus = table.Column<bool>(type: "bit", nullable: false),
                    RelocationAuthorized = table.Column<bool>(type: "bit", nullable: false),
                    DrugTestRequired = table.Column<bool>(type: "bit", nullable: false),
                    HiringOrganizationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Positions_HiringOrganizations_HiringOrganizationId",
                        column: x => x.HiringOrganizationId,
                        principalTable: "HiringOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PositionRemunerations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MinimumRange = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumRange = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RateIntervalCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PositionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionRemunerations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionRemunerations_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PositionRemunerations_PositionId",
                table: "PositionRemunerations",
                column: "PositionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_HiringOrganizationId",
                table: "Positions",
                column: "HiringOrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PositionRemunerations");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "HiringOrganizations");
        }
    }
}
