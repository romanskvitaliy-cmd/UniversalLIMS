using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversalLIMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OrganizationName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Edrpou = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Rnokpp = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsAnnulled = table.Column<bool>(type: "bit", nullable: false),
                    AnnulledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnulledByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnnulmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvestigationTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NameUk = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DescriptionUk = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsAnnulled = table.Column<bool>(type: "bit", nullable: false),
                    AnnulledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnulledByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnnulmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferralNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsAnnulled = table.Column<bool>(type: "bit", nullable: false),
                    AnnulledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnulledByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnnulmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestigationTypeTemplates",
                columns: table => new
                {
                    InvestigationTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationTypeTemplates", x => new { x.InvestigationTypeId, x.TemplateId });
                    table.ForeignKey(
                        name: "FK_InvestigationTypeTemplates_InvestigationTypes_InvestigationTypeId",
                        column: x => x.InvestigationTypeId,
                        principalTable: "InvestigationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestigationTypeTemplates_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Samples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InvestigationTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RoutedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsAnnulled = table.Column<bool>(type: "bit", nullable: false),
                    AnnulledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnulledByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnnulmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Samples_InvestigationTypes_InvestigationTypeId",
                        column: x => x.InvestigationTypeId,
                        principalTable: "InvestigationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Samples_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SampleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetBranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SentToLabAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsAnnulled = table.Column<bool>(type: "bit", nullable: false),
                    AnnulledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnulledByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnnulmentReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderDocuments_Branches_TargetBranchId",
                        column: x => x.TargetBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderDocuments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderDocuments_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderDocuments_TemplateVersions_TemplateVersionId",
                        column: x => x.TemplateVersionId,
                        principalTable: "TemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderDocuments_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SampleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValueText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderFieldValues_DataFields_DataFieldId",
                        column: x => x.DataFieldId,
                        principalTable: "DataFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderFieldValues_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderFieldValues_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ContactPhone",
                table: "Customers",
                column: "ContactPhone");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Edrpou",
                table: "Customers",
                column: "Edrpou",
                filter: "[Edrpou] IS NOT NULL AND [IsAnnulled] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_FullName",
                table: "Customers",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OrganizationName",
                table: "Customers",
                column: "OrganizationName");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Rnokpp",
                table: "Customers",
                column: "Rnokpp",
                filter: "[Rnokpp] IS NOT NULL AND [IsAnnulled] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationTypes_Code",
                table: "InvestigationTypes",
                column: "Code",
                unique: true,
                filter: "[IsAnnulled] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationTypeTemplates_InvestigationTypeId_SortOrder",
                table: "InvestigationTypeTemplates",
                columns: new[] { "InvestigationTypeId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationTypeTemplates_TemplateId",
                table: "InvestigationTypeTemplates",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDocuments_OrderId",
                table: "OrderDocuments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDocuments_SampleId_TemplateId",
                table: "OrderDocuments",
                columns: new[] { "SampleId", "TemplateId" },
                filter: "[IsAnnulled] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDocuments_TargetBranchId_Status",
                table: "OrderDocuments",
                columns: new[] { "TargetBranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderDocuments_TemplateId",
                table: "OrderDocuments",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDocuments_TemplateVersionId",
                table: "OrderDocuments",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFieldValues_DataFieldId",
                table: "OrderFieldValues",
                column: "DataFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFieldValues_OrderId_DataFieldId",
                table: "OrderFieldValues",
                columns: new[] { "OrderId", "DataFieldId" },
                unique: true,
                filter: "[SampleId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFieldValues_OrderId_SampleId_DataFieldId",
                table: "OrderFieldValues",
                columns: new[] { "OrderId", "SampleId", "DataFieldId" },
                unique: true,
                filter: "[SampleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderFieldValues_SampleId",
                table: "OrderFieldValues",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BranchId_Status",
                table: "Orders",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ReferralNumber",
                table: "Orders",
                column: "ReferralNumber",
                filter: "[ReferralNumber] IS NOT NULL AND [IsAnnulled] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_InvestigationTypeId",
                table: "Samples",
                column: "InvestigationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_Number",
                table: "Samples",
                column: "Number",
                filter: "[IsAnnulled] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OrderId_Number",
                table: "Samples",
                columns: new[] { "OrderId", "Number" },
                unique: true,
                filter: "[IsAnnulled] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestigationTypeTemplates");

            migrationBuilder.DropTable(
                name: "OrderDocuments");

            migrationBuilder.DropTable(
                name: "OrderFieldValues");

            migrationBuilder.DropTable(
                name: "Samples");

            migrationBuilder.DropTable(
                name: "InvestigationTypes");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
