using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RollerMillMonitor.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plcs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Protocol = table.Column<string>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Rack = table.Column<int>(type: "INTEGER", nullable: true),
                    Slot = table.Column<int>(type: "INTEGER", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConnectionStatus = table.Column<string>(type: "TEXT", nullable: false),
                    LastConnectedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plcs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Points",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlcId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    DataType = table.Column<string>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    Scale = table.Column<double>(type: "REAL", nullable: false),
                    Offset = table.Column<double>(type: "REAL", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AlarmEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    HighHigh = table.Column<double>(type: "REAL", nullable: true),
                    High = table.Column<double>(type: "REAL", nullable: true),
                    Low = table.Column<double>(type: "REAL", nullable: true),
                    LowLow = table.Column<double>(type: "REAL", nullable: true),
                    AlarmDelayMs = table.Column<int>(type: "INTEGER", nullable: false),
                    RecoveryDelayMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Deadband = table.Column<double>(type: "REAL", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Points", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Points_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Points_Plcs_PlcId",
                        column: x => x.PlcId,
                        principalTable: "Plcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Alarms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PointId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: true),
                    PlcId = table.Column<int>(type: "INTEGER", nullable: true),
                    AlarmType = table.Column<string>(type: "TEXT", nullable: false),
                    AlarmLevel = table.Column<string>(type: "TEXT", nullable: false),
                    PointName = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    AlarmValue = table.Column<double>(type: "REAL", nullable: true),
                    Threshold = table.Column<double>(type: "REAL", nullable: true),
                    AlarmTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecoveryTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false),
                    AckTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AckBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alarms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alarms_Points_PointId",
                        column: x => x.PointId,
                        principalTable: "Points",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trends",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PointId = table.Column<int>(type: "INTEGER", nullable: false),
                    PointName = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trends", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trends_Points_PointId",
                        column: x => x.PointId,
                        principalTable: "Points",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_IsActive_AlarmTime",
                table: "Alarms",
                columns: new[] { "IsActive", "AlarmTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_PointId_AlarmType_IsActive",
                table: "Alarms",
                columns: new[] { "PointId", "AlarmType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Points_DeviceId_Name",
                table: "Points",
                columns: new[] { "DeviceId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Points_DeviceId_PlcId",
                table: "Points",
                columns: new[] { "DeviceId", "PlcId" });

            migrationBuilder.CreateIndex(
                name: "IX_Points_PlcId",
                table: "Points",
                column: "PlcId");

            migrationBuilder.CreateIndex(
                name: "IX_Trends_PointId_Timestamp",
                table: "Trends",
                columns: new[] { "PointId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alarms");

            migrationBuilder.DropTable(
                name: "Trends");

            migrationBuilder.DropTable(
                name: "Points");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Plcs");
        }
    }
}
