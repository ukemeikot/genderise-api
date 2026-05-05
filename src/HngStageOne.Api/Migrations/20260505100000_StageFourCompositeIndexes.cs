using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HngStageOne.Api.Migrations
{
    [Migration("20260505100000_StageFourCompositeIndexes")]
    public partial class StageFourCompositeIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Profiles_Country_Gender_Age""
                ON ""Profiles"" (""CountryId"", ""Gender"", ""Age"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Profiles_Gender_AgeGroup""
                ON ""Profiles"" (""Gender"", ""AgeGroup"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Profiles_Country_AgeGroup""
                ON ""Profiles"" (""CountryId"", ""AgeGroup"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Profiles_CreatedAt_Id""
                ON ""Profiles"" (""CreatedAt"", ""Id"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Profiles_CreatedAt_Id"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Profiles_Country_AgeGroup"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Profiles_Gender_AgeGroup"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Profiles_Country_Gender_Age"";");
        }
    }
}
