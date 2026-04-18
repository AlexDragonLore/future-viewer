using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FutureViewer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGlossaryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "aliases",
                table: "tarot_cards",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_en",
                table: "tarot_cards",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "reversed_keywords",
                table: "tarot_cards",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "short_reversed",
                table: "tarot_cards",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "short_upright",
                table: "tarot_cards",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "suggested_tone",
                table: "tarot_cards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "upright_keywords",
                table: "tarot_cards",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateTable(
                name: "deck_variants",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    card_id = table.Column<int>(type: "integer", nullable: false),
                    deck_type = table.Column<int>(type: "integer", nullable: false),
                    variant_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deck_variants", x => x.id);
                    table.ForeignKey(
                        name: "FK_deck_variants_tarot_cards_card_id",
                        column: x => x.card_id,
                        principalTable: "tarot_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deck_variants_card_id_deck_type",
                table: "deck_variants",
                columns: new[] { "card_id", "deck_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deck_variants");

            migrationBuilder.DropColumn(
                name: "aliases",
                table: "tarot_cards");

            migrationBuilder.DropColumn(
                name: "name_en",
                table: "tarot_cards");

            migrationBuilder.DropColumn(
                name: "reversed_keywords",
                table: "tarot_cards");

            migrationBuilder.DropColumn(
                name: "short_reversed",
                table: "tarot_cards");

            migrationBuilder.DropColumn(
                name: "short_upright",
                table: "tarot_cards");

            migrationBuilder.DropColumn(
                name: "suggested_tone",
                table: "tarot_cards");

            migrationBuilder.DropColumn(
                name: "upright_keywords",
                table: "tarot_cards");
        }
    }
}
