using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace B4JScanner
{
    static class MdWriter
    {
        const string ToolVersion = "1.0";

        public static string Write(B4JProject project, List<ResolvedLibrary> libraries,
            List<JavaSourceFile> javaFiles, string outputPath)
        {
            int b4xFound = 0, b4xNotFound = 0;
            var b4xLibs   = new List<ResolvedLibrary>();
            var javaDeps  = new List<ResolvedLibrary>();
            var mavenDeps = new List<ResolvedDependency>();
            var seenPurls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var lib in libraries)
            {
                if (!IsB4X(lib))
                {
                    javaDeps.Add(lib);
                    continue;
                }
                b4xLibs.Add(lib);
                if (lib.Found) b4xFound++; else b4xNotFound++;
                if (lib.Info == null) continue;
                foreach (var dep in lib.Info.ResolvedDeps)
                {
                    if (dep.Maven == null) continue;
                    if (seenPurls.Add(dep.Maven.ToPurl()))
                        mavenDeps.Add(dep);
                }
            }

            int totalMavenDeps = javaDeps.Count + mavenDeps.Count;

            var sb = new StringBuilder();

            // Title
            sb.AppendLine("# SBOM Report: " + project.Name);
            sb.AppendLine();
            sb.AppendLine("> Generated " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC"
                        + " by B4JScanner v" + ToolVersion);
            sb.AppendLine();

            // Project info
            sb.AppendLine("## Project");
            sb.AppendLine();
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------|-------|");
            sb.AppendLine("| Name | " + Md(project.Name) + " |");
            sb.AppendLine("| Version | " + Md(project.Version ?? "unknown") + " |");
            if (!string.IsNullOrEmpty(project.JavaPackage))
                sb.AppendLine("| Package | `" + project.JavaPackage + "` |");
            sb.AppendLine("| B4J File | `" + project.ProjectFile + "` |");
            sb.AppendLine();

            // Summary
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine("| | Count |");
            sb.AppendLine("|---|------:|");
            sb.AppendLine("| B4X libraries | " + b4xLibs.Count + " |");
            sb.AppendLine("| Found | " + b4xFound + " |");
            if (b4xNotFound > 0)
                sb.AppendLine("| **Not found** | **" + b4xNotFound + "** |");
            sb.AppendLine("| Maven dependencies | " + totalMavenDeps + " |");
            sb.AppendLine("| Java source files scanned | " + javaFiles.Count + " |");
            sb.AppendLine();

            // B4X Libraries table
            sb.AppendLine("## B4X Libraries");
            sb.AppendLine();
            sb.AppendLine("| Library | Type | Version | Deps |");
            sb.AppendLine("|---------|------|---------|-----:|");

            foreach (var lib in b4xLibs)
            {
                string typeLabel = lib.XmlPath != null ? "B4X Jar" : "b4xlib";
                string status = lib.Found ? "" : " ⚠";
                var info = lib.Info;
                string ver   = info != null && !string.IsNullOrEmpty(info.Version) ? info.Version : "unknown";
                int depCount = info != null ? info.ResolvedDeps.Count : 0;
                string deps = depCount > 0 ? depCount.ToString() : "-";

                sb.AppendLine("| " + Md(lib.LibraryName) + status
                            + " | " + typeLabel
                            + " | " + Md(ver)
                            + " | " + deps + " |");
            }
            sb.AppendLine();

            // Maven dependencies table
            if (totalMavenDeps > 0)
            {
                sb.AppendLine("## Maven Dependencies");
                sb.AppendLine();
                sb.AppendLine("Underlying Java libraries from b4xlib dependencies, `#AdditionalJar` directives, and B4X `<dependsOn>` metadata.");
                sb.AppendLine("Run OSV Scan to check these for known vulnerabilities.");
                sb.AppendLine();
                sb.AppendLine("| Name | Group ID | Artifact ID | Version | Source | PURL |");
                sb.AppendLine("|------|----------|-------------|---------|--------|------|");

                // Native JARs (from b4xlib DependsOn expansion) and AdditionalJar entries
                foreach (var lib in javaDeps)
                {
                    var info = lib.Info;
                    string ver  = info != null && !string.IsNullOrEmpty(info.Version) ? info.Version : "unknown";
                    bool hasCoords = info != null && info.Maven != null && info.Maven.GroupId != null;
                    string gId  = hasCoords ? "`" + Md(info.Maven.GroupId)    + "`" : "-";
                    string aId  = hasCoords ? "`" + Md(info.Maven.ArtifactId) + "`" : "-";
                    string purl = hasCoords ? "`" + info.Maven.ToPurl() + "`"
                        : (info != null && info.Maven != null && info.Maven.Note != null)
                            ? Md(info.Maven.Note) : "-";
                    string src  = lib.IsAdditionalJar ? "AJ" : "b4xlib dep";

                    sb.AppendLine("| " + Md(lib.LibraryName)
                                + " | " + gId
                                + " | " + aId
                                + " | " + Md(ver)
                                + " | " + src
                                + " | " + purl + " |");
                }

                // ResolvedDeps from B4X Jar <dependsOn> XML entries
                mavenDeps.Sort((a, b) =>
                {
                    int c = string.Compare(a.Maven.GroupId, b.Maven.GroupId, StringComparison.OrdinalIgnoreCase);
                    return c != 0 ? c : string.Compare(a.Maven.ArtifactId, b.Maven.ArtifactId, StringComparison.OrdinalIgnoreCase);
                });

                foreach (var dep in mavenDeps)
                {
                    sb.AppendLine("| " + Md(dep.Name)
                                + " | `" + Md(dep.Maven.GroupId)    + "`"
                                + " | `" + Md(dep.Maven.ArtifactId) + "`"
                                + " | " + Md(dep.Maven.Version ?? "unknown")
                                + " | B4X dep"
                                + " | `" + dep.Maven.ToPurl() + "` |");
                }
                sb.AppendLine();
            }

            // Java import prefixes (if any)
            var prefixes = JavaSourceScanner.GetUniquePackagePrefixes(javaFiles);
            if (prefixes.Count > 0)
            {
                sb.AppendLine("## Java Source Import Prefixes");
                sb.AppendLine();
                sb.AppendLine("Third-party package prefixes found in generated `Objects/src` Java files.");
                sb.AppendLine();
                foreach (var p in prefixes)
                    sb.AppendLine("- `" + p + "`");
                sb.AppendLine();
            }

            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
            return outputPath;
        }

        static bool IsB4X(ResolvedLibrary lib)
        {
            return lib.XmlPath != null || lib.B4xlibPath != null;
        }

        // Escape pipe characters so they don't break Markdown tables
        static string Md(string value)
        {
            if (value == null) return "";
            return value.Replace("|", "\\|").Replace("[", "\\[").Replace("]", "\\]");
        }
    }
}
