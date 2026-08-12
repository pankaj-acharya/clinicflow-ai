using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicFlowAi.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "clinicians",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    ClinicId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinicians", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "appointment_slots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ClinicianId = table.Column<string>(type: "text", nullable: false),
                    ClinicId = table.Column<string>(type: "text", nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsBooked = table.Column<bool>(type: "boolean", nullable: false),
                    AppointmentTypeCode = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_appointment_slots_clinicians_ClinicianId",
                        column: x => x.ClinicianId,
                        principalTable: "clinicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "schedule_rules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ClinicianId = table.Column<string>(type: "text", nullable: false),
                    ClinicId = table.Column<string>(type: "text", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_schedule_rules_clinicians_ClinicianId",
                        column: x => x.ClinicianId,
                        principalTable: "clinicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SlotId = table.Column<string>(type: "text", nullable: false),
                    PatientReferenceId = table.Column<string>(type: "text", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bookings_appointment_slots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "appointment_slots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_slots_ClinicianId_StartsAtUtc",
                table: "appointment_slots",
                columns: new[] { "ClinicianId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_SlotId",
                table: "bookings",
                column: "SlotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedule_rules_ClinicianId",
                table: "schedule_rules",
                column: "ClinicianId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "schedule_rules");

            migrationBuilder.DropTable(
                name: "appointment_slots");

            migrationBuilder.DropTable(
                name: "clinicians");
        }
    }
}
