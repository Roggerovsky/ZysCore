using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DiscordAutomation.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IconUrl = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PremiumTier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PremiumExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ChannelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    RelatedRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionTaken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Logs_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Modules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Configuration = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Modules_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsPremiumOnly = table.Column<bool>(type: "boolean", nullable: false),
                    TriggerType = table.Column<int>(type: "integer", nullable: false),
                    TriggerConditions = table.Column<string>(type: "jsonb", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    ActionParameters = table.Column<string>(type: "jsonb", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "integer", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rules_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rules_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_CreatedAt",
                table: "Guilds",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_IsActive",
                table: "Guilds",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_Name",
                table: "Guilds",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_PremiumTier",
                table: "Guilds",
                column: "PremiumTier");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_GuildId_CreatedAt",
                table: "Logs",
                columns: new[] { "GuildId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Logs_Severity",
                table: "Logs",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_Source",
                table: "Logs",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Modules_GuildId_Type",
                table: "Modules",
                columns: new[] { "GuildId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Modules_IsEnabled",
                table: "Modules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_CreatedAt",
                table: "Rules",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_GuildId_ModuleId",
                table: "Rules",
                columns: new[] { "GuildId", "ModuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Rules_IsEnabled",
                table: "Rules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_ModuleId",
                table: "Rules",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_Priority",
                table: "Rules",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Logs");

            migrationBuilder.DropTable(
                name: "Rules");

            migrationBuilder.DropTable(
                name: "Modules");

            migrationBuilder.DropTable(
                name: "Guilds");
        }
    }
}
