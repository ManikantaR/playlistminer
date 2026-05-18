using Microsoft.EntityFrameworkCore;
using PlaylistMiner.Core;
using PlaylistMiner.Core.Models;

namespace PlaylistMiner.Infrastructure.Data;

internal static class SeedData
{
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void Apply(ModelBuilder modelBuilder)
    {
        var tags = BuildTags();
        modelBuilder.Entity<Tag>().HasData(tags);

        var rules = BuildTagRules(tags);
        modelBuilder.Entity<TagRule>().HasData(rules);
    }

    private static Tag[] BuildTags()
    {
        var defs = new[]
        {
            // Languages
            (1,  "C#",                 "Languages"),
            (2,  "Python",             "Languages"),
            (3,  "JavaScript",         "Languages"),
            (4,  "TypeScript",         "Languages"),
            (5,  "Go",                 "Languages"),
            (6,  "Rust",               "Languages"),
            (7,  "Java",               "Languages"),
            (8,  "SQL",                "Languages"),

            // Frontend
            (9,  "React",              "Frontend"),
            (10, "Next.js",            "Frontend"),
            (11, "Angular",            "Frontend"),
            (12, "Vue",                "Frontend"),
            (13, "Tailwind CSS",       "Frontend"),
            (14, "HTML/CSS",           "Frontend"),

            // Backend / Frameworks
            (15, "ASP.NET Core",       "Backend"),
            (16, "Node.js",            "Backend"),
            (17, "Django",             "Backend"),
            (18, "FastAPI",            "Backend"),

            // Cloud & DevOps
            (19, "AWS",                "Cloud"),
            (20, "Azure",              "Cloud"),
            (21, "GCP",                "Cloud"),
            (22, "Firebase",           "Cloud"),
            (23, "Terraform",          "DevOps"),
            (24, "Kubernetes",         "DevOps"),
            (25, "Docker",             "DevOps"),
            (26, "Podman",             "DevOps"),
            (27, "CI/CD",              "DevOps"),
            (28, "GitHub Actions",     "DevOps"),

            // Databases
            (29, "SQL Server",         "Databases"),
            (30, "PostgreSQL",         "Databases"),
            (31, "MongoDB",            "Databases"),
            (32, "Redis",              "Databases"),

            // Tools & Practices
            (33, "EF Core",            "Tools"),
            (34, "OAuth",              "Tools"),
            (35, "JWT",                "Tools"),
            (36, "Git",                "Tools"),
            (37, "GitHub",             "Tools"),
            (38, "GitHub Copilot",     "Tools"),
            (39, "VS Code",            "Tools"),

            // Architecture
            (40, "Microservices",      "Architecture"),
            (41, "Clean Architecture", "Architecture"),
            (42, "DDD",                "Architecture"),

            // AI/ML
            (43, "Machine Learning",   "AI"),
            (44, "LLMs",               "AI"),
            (45, "Prompt Engineering", "AI"),
        };

        return defs.Select(d => new Tag
        {
            Id = d.Item1,
            Name = d.Item2,
            Slug = SlugGenerator.Generate(d.Item2),
            Category = d.Item3,
            CreatedAt = SeedDate
        }).ToArray();
    }

    private static TagRule[] BuildTagRules(Tag[] tags)
    {
        var tagMap = tags.ToDictionary(t => t.Name, t => t.Id);

        var rulesData = new[]
        {
            ("C#",                 new[] { "c#", "csharp", "dotnet", ".net", "roslyn" }),
            ("Python",             new[] { "python", "py", "cpython", "pypi" }),
            ("JavaScript",         new[] { "javascript", "js", "ecmascript", "es6", "es2015" }),
            ("TypeScript",         new[] { "typescript", "ts", "tsc" }),
            ("Go",                 new[] { "golang", " go ", "gopher" }),
            ("Rust",               new[] { "rust", "rustlang", "cargo", "rustacean" }),
            ("Java",               new[] { "java", "jvm", "spring", "maven" }),
            ("SQL",                new[] { "sql", "query", "t-sql", "pl/sql" }),

            ("React",              new[] { "react", "reactjs", "react.js", "hooks", "jsx" }),
            ("Next.js",            new[] { "next.js", "nextjs", "next js", "vercel" }),
            ("Angular",            new[] { "angular", "angularjs", "ng", "ngrx" }),
            ("Vue",                new[] { "vue", "vuejs", "vue.js", "nuxt" }),
            ("Tailwind CSS",       new[] { "tailwind", "tailwindcss", "utility css" }),
            ("HTML/CSS",           new[] { "html", "css", "html5", "sass", "scss" }),

            ("ASP.NET Core",       new[] { "asp.net", "aspnet", "asp.net core", "blazor", "minimal api" }),
            ("Node.js",            new[] { "node.js", "nodejs", "node js", "express", "npm" }),
            ("Django",             new[] { "django", "django rest", "drf" }),
            ("FastAPI",            new[] { "fastapi", "fast api", "pydantic" }),

            ("AWS",                new[] { "aws", "amazon web services", "ec2", "s3", "lambda" }),
            ("Azure",              new[] { "azure", "microsoft azure", "azure devops" }),
            ("GCP",                new[] { "gcp", "google cloud", "bigquery" }),
            ("Firebase",           new[] { "firebase", "firestore", "google firebase" }),
            ("Terraform",          new[] { "terraform", "hcl", "infrastructure as code", "iac" }),
            ("Kubernetes",         new[] { "kubernetes", "k8s", "kubectl", "helm" }),
            ("Docker",             new[] { "docker", "dockerfile", "docker-compose", "container" }),
            ("Podman",             new[] { "podman", "podman-compose" }),
            ("CI/CD",              new[] { "ci/cd", "pipeline", "continuous integration", "continuous deployment" }),
            ("GitHub Actions",     new[] { "github actions", "actions", "workflow yml" }),

            ("SQL Server",         new[] { "sql server", "mssql", "microsoft sql" }),
            ("PostgreSQL",         new[] { "postgresql", "postgres", "pg" }),
            ("MongoDB",            new[] { "mongodb", "mongo", "nosql document" }),
            ("Redis",              new[] { "redis", "cache", "pub/sub" }),

            ("EF Core",            new[] { "entity framework", "ef core", "efcore", "dbcontext" }),
            ("OAuth",              new[] { "oauth", "oauth2", "openid", "authorization" }),
            ("JWT",                new[] { "jwt", "json web token", "bearer token" }),
            ("Git",                new[] { "git", "version control", "branching" }),
            ("GitHub",             new[] { "github", "pull request", "repository" }),
            ("GitHub Copilot",     new[] { "github copilot", "copilot", "ai pair programming" }),
            ("VS Code",            new[] { "vs code", "vscode", "visual studio code" }),

            ("Microservices",      new[] { "microservices", "microservice", "service mesh" }),
            ("Clean Architecture", new[] { "clean architecture", "onion architecture", "hexagonal" }),
            ("DDD",                new[] { "ddd", "domain driven design", "domain-driven", "bounded context" }),

            ("Machine Learning",   new[] { "machine learning", "ml", "deep learning", "neural network" }),
            ("LLMs",               new[] { "llm", "large language model", "gpt", "llama" }),
            ("Prompt Engineering", new[] { "prompt engineering", "prompt", "system prompt" }),
        };

        var rules = new List<TagRule>();
        var id = 1;

        foreach (var (tagName, keywords) in rulesData)
        {
            if (!tagMap.TryGetValue(tagName, out var tagId))
            {
                continue;
            }

            foreach (var keyword in keywords)
            {
                rules.Add(new TagRule
                {
                    Id = id++,
                    TagId = tagId,
                    Keyword = keyword,
                    Field = TagRuleField.Both,
                    Weight = 0.5f,
                    IsLearned = false,
                    CreatedAt = SeedDate,
                    UpdatedAt = SeedDate
                });
            }
        }

        return [.. rules];
    }
}
