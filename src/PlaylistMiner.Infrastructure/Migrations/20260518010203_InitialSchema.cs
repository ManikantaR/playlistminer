using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PlaylistMiner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    filename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    total_videos = table.Column<int>(type: "integer", nullable: false),
                    imported_count = table.Column<int>(type: "integer", nullable: false),
                    failed_count = table.Column<int>(type: "integer", nullable: false),
                    imported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "playlists",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    youtube_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_inbox = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_managed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    sync_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    videos_processed = table.Column<int>(type: "integer", nullable: false),
                    videos_categorized = table.Column<int>(type: "integer", nullable: false),
                    errors = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "videos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    youtube_id = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    channel_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    channel_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    thumbnail_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_videos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tag_rules",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tag_id = table.Column<int>(type: "integer", nullable: false),
                    keyword = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    field = table.Column<string>(type: "text", nullable: false),
                    weight = table.Column<float>(type: "real", nullable: false, defaultValue: 0.5f),
                    is_learned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tag_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_tag_rules_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlist_videos",
                columns: table => new
                {
                    playlist_id = table.Column<int>(type: "integer", nullable: false),
                    video_id = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlist_videos", x => new { x.playlist_id, x.video_id });
                    table.ForeignKey(
                        name: "FK_playlist_videos_playlists_playlist_id",
                        column: x => x.playlist_id,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_playlist_videos_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "undo_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    video_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_playlist_id = table.Column<int>(type: "integer", nullable: true),
                    target_playlist_id = table.Column<int>(type: "integer", nullable: true),
                    performed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    undone = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_undo_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_undo_logs_playlists_source_playlist_id",
                        column: x => x.source_playlist_id,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_undo_logs_playlists_target_playlist_id",
                        column: x => x.target_playlist_id,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_undo_logs_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "video_tags",
                columns: table => new
                {
                    video_id = table.Column<int>(type: "integer", nullable: false),
                    tag_id = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<float>(type: "real", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_tags", x => new { x.video_id, x.tag_id, x.source });
                    table.ForeignKey(
                        name: "FK_video_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_video_tags_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "tags",
                columns: new[] { "id", "category", "created_at", "name", "slug" },
                values: new object[,]
                {
                    { 1, "Languages", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "C#", "csharp" },
                    { 2, "Languages", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Python", "python" },
                    { 3, "Languages", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "JavaScript", "javascript" },
                    { 4, "Languages", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "TypeScript", "typescript" },
                    { 5, "Languages", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Go", "go" },
                    { 6, "Languages", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Rust", "rust" },
                    { 7, "Languages", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Java", "java" },
                    { 8, "Languages", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SQL", "sql" },
                    { 9, "Frontend", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "React", "react" },
                    { 10, "Frontend", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Next.js", "nextjs" },
                    { 11, "Frontend", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Angular", "angular" },
                    { 12, "Frontend", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Vue", "vue" },
                    { 13, "Frontend", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Tailwind CSS", "tailwind-css" },
                    { 14, "Frontend", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "HTML/CSS", "html-css" },
                    { 15, "Backend", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "ASP.NET Core", "aspnet-core" },
                    { 16, "Backend", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Node.js", "nodejs" },
                    { 17, "Backend", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Django", "django" },
                    { 18, "Backend", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "FastAPI", "fastapi" },
                    { 19, "Cloud", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AWS", "aws" },
                    { 20, "Cloud", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Azure", "azure" },
                    { 21, "Cloud", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GCP", "gcp" },
                    { 22, "Cloud", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Firebase", "firebase" },
                    { 23, "DevOps", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Terraform", "terraform" },
                    { 24, "DevOps", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Kubernetes", "kubernetes" },
                    { 25, "DevOps", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Docker", "docker" },
                    { 26, "DevOps", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Podman", "podman" },
                    { 27, "DevOps", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "CI/CD", "ci-cd" },
                    { 28, "DevOps", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GitHub Actions", "github-actions" },
                    { 29, "Databases", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SQL Server", "sql-server" },
                    { 30, "Databases", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "PostgreSQL", "postgresql" },
                    { 31, "Databases", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "MongoDB", "mongodb" },
                    { 32, "Databases", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Redis", "redis" },
                    { 33, "Tools", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "EF Core", "ef-core" },
                    { 34, "Tools", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OAuth", "oauth" },
                    { 35, "Tools", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "JWT", "jwt" },
                    { 36, "Tools", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Git", "git" },
                    { 37, "Tools", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GitHub", "github" },
                    { 38, "Tools", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GitHub Copilot", "github-copilot" },
                    { 39, "Tools", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VS Code", "vs-code" },
                    { 40, "Architecture", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Microservices", "microservices" },
                    { 41, "Architecture", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Clean Architecture", "clean-architecture" },
                    { 42, "Architecture", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "DDD", "ddd" },
                    { 43, "AI", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Machine Learning", "machine-learning" },
                    { 44, "AI", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "LLMs", "llms" },
                    { 45, "AI", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Prompt Engineering", "prompt-engineering" }
                });

            migrationBuilder.InsertData(
                table: "tag_rules",
                columns: new[] { "id", "created_at", "field", "keyword", "tag_id", "updated_at", "weight" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "c#", 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "csharp", 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "dotnet", 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 4, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", ".net", 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 5, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "roslyn", 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 6, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "python", 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 7, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "py", 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 8, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "cpython", 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 9, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "pypi", 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 10, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "javascript", 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 11, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "js", 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 12, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "ecmascript", 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 13, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "es6", 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 14, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "es2015", 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 15, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "typescript", 4, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 16, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "ts", 4, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 17, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "tsc", 4, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 18, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "golang", 5, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 19, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", " go ", 5, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 20, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "gopher", 5, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 21, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "rust", 6, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 22, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "rustlang", 6, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 23, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "cargo", 6, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 24, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "rustacean", 6, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 25, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "java", 7, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 26, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "jvm", 7, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 27, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "spring", 7, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 28, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "maven", 7, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 29, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "sql", 8, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 30, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "query", 8, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 31, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "t-sql", 8, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 32, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "pl/sql", 8, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 33, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "react", 9, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 34, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "reactjs", 9, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 35, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "react.js", 9, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 36, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "hooks", 9, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 37, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "jsx", 9, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 38, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "next.js", 10, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 39, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "nextjs", 10, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 40, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "next js", 10, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 41, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "vercel", 10, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 42, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "angular", 11, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 43, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "angularjs", 11, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 44, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "ng", 11, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 45, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "ngrx", 11, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 46, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "vue", 12, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 47, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "vuejs", 12, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 48, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "vue.js", 12, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 49, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "nuxt", 12, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 50, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "tailwind", 13, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 51, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "tailwindcss", 13, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 52, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "utility css", 13, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 53, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "html", 14, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 54, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "css", 14, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 55, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "html5", 14, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 56, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "sass", 14, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 57, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "scss", 14, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 58, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "asp.net", 15, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 59, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "aspnet", 15, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 60, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "asp.net core", 15, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 61, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "blazor", 15, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 62, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "minimal api", 15, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 63, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "node.js", 16, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 64, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "nodejs", 16, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 65, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "node js", 16, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 66, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "express", 16, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 67, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "npm", 16, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 68, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "django", 17, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 69, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "django rest", 17, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 70, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "drf", 17, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 71, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "fastapi", 18, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 72, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "fast api", 18, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 73, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "pydantic", 18, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 74, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "aws", 19, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 75, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "amazon web services", 19, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 76, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "ec2", 19, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 77, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "s3", 19, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 78, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "lambda", 19, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 79, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "azure", 20, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 80, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "microsoft azure", 20, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 81, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "azure devops", 20, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 82, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "gcp", 21, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 83, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "google cloud", 21, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 84, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "bigquery", 21, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 85, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "firebase", 22, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 86, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "firestore", 22, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 87, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "google firebase", 22, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 88, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "terraform", 23, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 89, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "hcl", 23, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 90, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "infrastructure as code", 23, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 91, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "iac", 23, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 92, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "kubernetes", 24, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 93, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "k8s", 24, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 94, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "kubectl", 24, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 95, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "helm", 24, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 96, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "docker", 25, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 97, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "dockerfile", 25, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 98, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "docker-compose", 25, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 99, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "container", 25, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 100, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "podman", 26, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 101, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "podman-compose", 26, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 102, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "ci/cd", 27, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 103, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "pipeline", 27, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 104, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "continuous integration", 27, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 105, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "continuous deployment", 27, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 106, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "github actions", 28, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 107, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "actions", 28, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 108, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "workflow yml", 28, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 109, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "sql server", 29, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 110, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "mssql", 29, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 111, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "microsoft sql", 29, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 112, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "postgresql", 30, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 113, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "postgres", 30, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 114, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "pg", 30, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 115, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "mongodb", 31, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 116, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "mongo", 31, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 117, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "nosql document", 31, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 118, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "redis", 32, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 119, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "cache", 32, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 120, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "pub/sub", 32, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 121, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "entity framework", 33, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 122, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "ef core", 33, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 123, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "efcore", 33, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 124, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "dbcontext", 33, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 125, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "oauth", 34, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 126, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "oauth2", 34, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 127, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "openid", 34, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 128, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "authorization", 34, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 129, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "jwt", 35, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 130, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "json web token", 35, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 131, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "bearer token", 35, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 132, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "git", 36, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 133, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "version control", 36, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 134, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "branching", 36, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 135, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "github", 37, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 136, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "pull request", 37, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 137, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "repository", 37, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 138, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "github copilot", 38, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 139, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "copilot", 38, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 140, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "ai pair programming", 38, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 141, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "vs code", 39, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 142, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "vscode", 39, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 143, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "visual studio code", 39, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 144, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "microservices", 40, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 145, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "microservice", 40, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 146, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "service mesh", 40, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 147, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "clean architecture", 41, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 148, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "onion architecture", 41, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 149, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "hexagonal", 41, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 150, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "ddd", 42, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 151, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "domain driven design", 42, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 152, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "domain-driven", 42, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 153, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "bounded context", 42, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 154, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "machine learning", 43, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 155, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "ml", 43, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 156, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "deep learning", 43, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 157, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "neural network", 43, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 158, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "llm", 44, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 159, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "large language model", 44, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 160, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "gpt", 44, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 161, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "llama", 44, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 162, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "prompt engineering", 45, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 163, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "prompt", 45, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f },
                    { 164, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Both", "system prompt", 45, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0.5f }
                });

            migrationBuilder.CreateIndex(
                name: "IX_playlist_videos_video_id",
                table: "playlist_videos",
                column: "video_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlists_youtube_id",
                table: "playlists",
                column: "youtube_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tag_rules_tag_id",
                table: "tag_rules",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_tags_name",
                table: "tags",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tags_slug",
                table: "tags",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_undo_logs_source_playlist_id",
                table: "undo_logs",
                column: "source_playlist_id");

            migrationBuilder.CreateIndex(
                name: "IX_undo_logs_target_playlist_id",
                table: "undo_logs",
                column: "target_playlist_id");

            migrationBuilder.CreateIndex(
                name: "IX_undo_logs_video_id",
                table: "undo_logs",
                column: "video_id");

            migrationBuilder.CreateIndex(
                name: "IX_video_tags_tag_id",
                table: "video_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_videos_title_fulltext",
                table: "videos",
                column: "title")
                .Annotation("Npgsql:IndexMethod", "GiST")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_videos_youtube_id",
                table: "videos",
                column: "youtube_id",
                unique: true);

            // Enable pg_trgm extension and create proper trigram / full-text indexes
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(
                "CREATE INDEX ix_videos_title_trigram ON videos USING GIN (title gin_trgm_ops);");

            migrationBuilder.Sql(
                "CREATE INDEX ix_videos_fts ON videos USING GiST (to_tsvector('english', title));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_batches");

            migrationBuilder.DropTable(
                name: "playlist_videos");

            migrationBuilder.DropTable(
                name: "sync_logs");

            migrationBuilder.DropTable(
                name: "tag_rules");

            migrationBuilder.DropTable(
                name: "undo_logs");

            migrationBuilder.DropTable(
                name: "video_tags");

            migrationBuilder.DropTable(
                name: "playlists");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "videos");

            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_videos_title_trigram;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_videos_fts;");
        }
    }
}
