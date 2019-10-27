using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Broker.Common.Migrations
{
    public partial class Broker : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MySetups",
                columns: table => new
                {
                    Key = table.Column<string>(nullable: false),
                    Value = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MySetups", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "MyWebAPISettings",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Asset = table.Column<string>(nullable: true),
                    Currency = table.Column<string>(nullable: true),
                    Separator = table.Column<string>(nullable: true),
                    PrecisionAsset = table.Column<int>(nullable: false),
                    PrecisionCurrency = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyWebAPISettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MyCandles",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(nullable: false),
                    Open = table.Column<decimal>(nullable: false),
                    Close = table.Column<decimal>(nullable: false),
                    High = table.Column<decimal>(nullable: false),
                    Low = table.Column<decimal>(nullable: false),
                    SettingsId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyCandles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyCandles_MyWebAPISettings_SettingsId",
                        column: x => x.SettingsId,
                        principalTable: "MyWebAPISettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MyOrders",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderId = table.Column<string>(nullable: true),
                    Creation = table.Column<ulong>(nullable: false),
                    Completed = table.Column<ulong>(nullable: false),
                    Type = table.Column<int>(nullable: false),
                    State = table.Column<int>(nullable: false),
                    Price = table.Column<decimal>(nullable: false),
                    Volume = table.Column<decimal>(nullable: false),
                    Fee = table.Column<decimal>(nullable: false),
                    SettingsId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyOrders_MyWebAPISettings_SettingsId",
                        column: x => x.SettingsId,
                        principalTable: "MyWebAPISettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MyTickers",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<ulong>(nullable: false),
                    Ask = table.Column<decimal>(nullable: false),
                    Bid = table.Column<decimal>(nullable: false),
                    Volume = table.Column<decimal>(nullable: false),
                    LastTrade = table.Column<decimal>(nullable: false),
                    SettingsId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyTickers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyTickers_MyWebAPISettings_SettingsId",
                        column: x => x.SettingsId,
                        principalTable: "MyWebAPISettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MyBalances",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(nullable: false),
                    Asset = table.Column<string>(nullable: true),
                    Amount = table.Column<decimal>(nullable: false),
                    Reserved = table.Column<decimal>(nullable: false),
                    ToEuro = table.Column<decimal>(nullable: false),
                    SettingsId = table.Column<int>(nullable: true),
                    CandleId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyBalances_MyCandles_CandleId",
                        column: x => x.CandleId,
                        principalTable: "MyCandles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MyBalances_MyWebAPISettings_SettingsId",
                        column: x => x.SettingsId,
                        principalTable: "MyWebAPISettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MyMACDs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<ulong>(nullable: false),
                    FastValue = table.Column<decimal>(nullable: false),
                    SlowValue = table.Column<decimal>(nullable: false),
                    SignalValue = table.Column<decimal>(nullable: false),
                    MACD = table.Column<decimal>(nullable: false),
                    Hist = table.Column<decimal>(nullable: false),
                    CandleId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyMACDs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyMACDs_MyCandles_CandleId",
                        column: x => x.CandleId,
                        principalTable: "MyCandles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MyMomentums",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<ulong>(nullable: false),
                    MomentumValue = table.Column<decimal>(nullable: false),
                    CandleId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyMomentums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyMomentums_MyCandles_CandleId",
                        column: x => x.CandleId,
                        principalTable: "MyCandles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MyRSIs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<ulong>(nullable: false),
                    RSIValue = table.Column<decimal>(nullable: false),
                    CandleId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyRSIs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyRSIs_MyCandles_CandleId",
                        column: x => x.CandleId,
                        principalTable: "MyCandles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MyBalances_CandleId",
                table: "MyBalances",
                column: "CandleId");

            migrationBuilder.CreateIndex(
                name: "IX_MyBalances_SettingsId",
                table: "MyBalances",
                column: "SettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_MyCandles_SettingsId",
                table: "MyCandles",
                column: "SettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_MyMACDs_CandleId",
                table: "MyMACDs",
                column: "CandleId");

            migrationBuilder.CreateIndex(
                name: "IX_MyMomentums_CandleId",
                table: "MyMomentums",
                column: "CandleId");

            migrationBuilder.CreateIndex(
                name: "IX_MyOrders_SettingsId",
                table: "MyOrders",
                column: "SettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_MyRSIs_CandleId",
                table: "MyRSIs",
                column: "CandleId");

            migrationBuilder.CreateIndex(
                name: "IX_MyTickers_SettingsId",
                table: "MyTickers",
                column: "SettingsId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MyBalances");

            migrationBuilder.DropTable(
                name: "MyMACDs");

            migrationBuilder.DropTable(
                name: "MyMomentums");

            migrationBuilder.DropTable(
                name: "MyOrders");

            migrationBuilder.DropTable(
                name: "MyRSIs");

            migrationBuilder.DropTable(
                name: "MySetups");

            migrationBuilder.DropTable(
                name: "MyTickers");

            migrationBuilder.DropTable(
                name: "MyCandles");

            migrationBuilder.DropTable(
                name: "MyWebAPISettings");
        }
    }
}
