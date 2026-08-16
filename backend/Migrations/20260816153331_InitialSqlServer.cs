using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "canned_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    shortcut = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canned_responses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    sender_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    sender_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    assigned_agent = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    human_only = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    embedding_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    metadata_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_base", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    plan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    anthropic_api_key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    openai_api_key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "escalations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ai_summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    suggested_response = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    resolved = table.Column<bool>(type: "bit", nullable: false),
                    resolved_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_escalations", x => x.id);
                    table.ForeignKey(
                        name: "FK_escalations_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "internal_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    author_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_internal_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_internal_notes_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    metadata_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversation_tags",
                columns: table => new
                {
                    conversation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    tag_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_tags", x => new { x.conversation_id, x.tag_id });
                    table.ForeignKey(
                        name: "FK_conversation_tags_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_conversation_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_api_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    team_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    key_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    prefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_api_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_api_keys_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_gmail_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    team_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    google_client_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    google_client_secret = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    credentials_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    watch_email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    last_poll_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_gmail_configs", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_gmail_configs_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_messenger_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    team_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    page_access_token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    page_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    verify_token = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    instagram_enabled = table.Column<bool>(type: "bit", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_messenger_configs", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_messenger_configs_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_telegram_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    team_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    bot_token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    bot_username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_telegram_configs", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_telegram_configs_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_whatsapp_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    team_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    phone_number_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    access_token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    verify_token = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_whatsapp_configs", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_whatsapp_configs_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    team_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_memberships_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_canned_responses_shortcut",
                table: "canned_responses",
                column: "shortcut",
                unique: true,
                filter: "[shortcut] IS NOT NULL AND [shortcut] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_tags_tag_id",
                table: "conversation_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_sender_id_channel",
                table: "conversations",
                columns: new[] { "sender_id", "channel" },
                unique: true,
                filter: "[status] IN ('active', 'escalated')");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_sender_id_channel_status",
                table: "conversations",
                columns: new[] { "sender_id", "channel", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_escalations_conversation_id",
                table: "escalations",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_internal_notes_conversation_id",
                table: "internal_notes",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_conversation_id",
                table: "messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_tags_name",
                table: "tags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_api_keys_key_hash",
                table: "team_api_keys",
                column: "key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_api_keys_team_id",
                table: "team_api_keys",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_gmail_configs_team_id",
                table: "team_gmail_configs",
                column: "team_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_team_id",
                table: "team_memberships",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_user_id_team_id",
                table: "team_memberships",
                columns: new[] { "user_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_messenger_configs_team_id",
                table: "team_messenger_configs",
                column: "team_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_telegram_configs_team_id",
                table: "team_telegram_configs",
                column: "team_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_whatsapp_configs_team_id",
                table: "team_whatsapp_configs",
                column: "team_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teams_slug",
                table: "teams",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "canned_responses");

            migrationBuilder.DropTable(
                name: "conversation_tags");

            migrationBuilder.DropTable(
                name: "escalations");

            migrationBuilder.DropTable(
                name: "internal_notes");

            migrationBuilder.DropTable(
                name: "knowledge_base");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "team_api_keys");

            migrationBuilder.DropTable(
                name: "team_gmail_configs");

            migrationBuilder.DropTable(
                name: "team_memberships");

            migrationBuilder.DropTable(
                name: "team_messenger_configs");

            migrationBuilder.DropTable(
                name: "team_telegram_configs");

            migrationBuilder.DropTable(
                name: "team_whatsapp_configs");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "teams");
        }
    }
}
