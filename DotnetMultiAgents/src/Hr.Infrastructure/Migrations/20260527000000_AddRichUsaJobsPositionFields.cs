using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hr.Infrastructure.Migrations
{
    public partial class AddRichUsaJobsPositionFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "AdditionalInformation", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "AdjudicationType", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "AnnouncementNumber", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ApplyUri", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ConditionsOfEmployment", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ContactAddress", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ContactEmail", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ContactName", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ContactPhone", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "DutyLocationState", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Education", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Evaluations", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<bool>(name: "FinancialDisclosure", table: "Positions", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "HiringPath", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "HowToApply", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "KeyRequirements", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "NextSteps", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "OccupationalSeriesTitle", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PositionOfferingType", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PositionSensitivityAndRisk", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PositionUri", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "PromotionPotential", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<bool>(name: "RemoteEligible", table: "Positions", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "RequiredDocuments", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "ServiceType", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "SubAgencyName", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "TotalOpenings", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "UsaJobsId", table: "Positions", type: "nvarchar(max)", nullable: false, defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AdditionalInformation", table: "Positions");
            migrationBuilder.DropColumn(name: "AdjudicationType", table: "Positions");
            migrationBuilder.DropColumn(name: "AnnouncementNumber", table: "Positions");
            migrationBuilder.DropColumn(name: "ApplyUri", table: "Positions");
            migrationBuilder.DropColumn(name: "ConditionsOfEmployment", table: "Positions");
            migrationBuilder.DropColumn(name: "ContactAddress", table: "Positions");
            migrationBuilder.DropColumn(name: "ContactEmail", table: "Positions");
            migrationBuilder.DropColumn(name: "ContactName", table: "Positions");
            migrationBuilder.DropColumn(name: "ContactPhone", table: "Positions");
            migrationBuilder.DropColumn(name: "DutyLocationState", table: "Positions");
            migrationBuilder.DropColumn(name: "Education", table: "Positions");
            migrationBuilder.DropColumn(name: "Evaluations", table: "Positions");
            migrationBuilder.DropColumn(name: "FinancialDisclosure", table: "Positions");
            migrationBuilder.DropColumn(name: "HiringPath", table: "Positions");
            migrationBuilder.DropColumn(name: "HowToApply", table: "Positions");
            migrationBuilder.DropColumn(name: "KeyRequirements", table: "Positions");
            migrationBuilder.DropColumn(name: "NextSteps", table: "Positions");
            migrationBuilder.DropColumn(name: "OccupationalSeriesTitle", table: "Positions");
            migrationBuilder.DropColumn(name: "PositionOfferingType", table: "Positions");
            migrationBuilder.DropColumn(name: "PositionSensitivityAndRisk", table: "Positions");
            migrationBuilder.DropColumn(name: "PositionUri", table: "Positions");
            migrationBuilder.DropColumn(name: "PromotionPotential", table: "Positions");
            migrationBuilder.DropColumn(name: "RemoteEligible", table: "Positions");
            migrationBuilder.DropColumn(name: "RequiredDocuments", table: "Positions");
            migrationBuilder.DropColumn(name: "ServiceType", table: "Positions");
            migrationBuilder.DropColumn(name: "SubAgencyName", table: "Positions");
            migrationBuilder.DropColumn(name: "TotalOpenings", table: "Positions");
            migrationBuilder.DropColumn(name: "UsaJobsId", table: "Positions");
        }
    }
}
