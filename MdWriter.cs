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
            // Count totals
            int found = 0, notFound = 0;
            var mavenDeps = new List<ResolvedDependency>();
            var seenPurls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var lib in libraries)
            {
                if (lib.Found) found++; else notFound++;
                if (lib.Info == null) continue;
                foreach (var dep in lib.Info.ResolvedDeps)
                {
                    if (dep.Maven == null) continue;
                    if (seenPurls.Add(dep.Maven.ToPurl()))
                        mavenDeps.Add(dep);
                }
            }

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
            sb.AppendLine("| B4J libraries | " + libraries.Count + " |");
            sb.AppendLine("| Found | " + found + " |");
            if (notFound > 0)
                sb.AppendLine("| **Not found** | **" + notFound + "** |");
            sb.AppendLine("| Maven dependencies identified | " + mavenDeps.Count + " |");
            sb.AppendLine("| Java source files scanned | " + javaFiles.Count + " |");
            sb.AppendLine();

            // B4J libraries table
            sb.AppendLine("## B4J Libraries");
            sb.AppendLine();
            sb.AppendLine("| Library | Version | Maven Coords | Deps |");
            sb.AppendLine("|---------|---------|--------------|-----:|");

            foreach (var lib in libraries)
            {
                string status = lib.Found ? "" : " ⚠";
                if (lib.IsAdditionalJar) status = " `[AJ]`" + status;
                var info = lib.Info;
                string ver  = info != null && !string.IsNullOrEmpty(info.Version) ? info.Version : "unknown";
                string maven = info != null && info.Maven != null
                    ? "`" + info.Maven.GroupId + ":" + info.Maven.ArtifactId + "`"
                    : "-";
                int depCount = info != null ? info.ResolvedDeps.Count : 0;
                string deps = depCount > 0 ? depCount.ToString() : "-";

                sb.AppendLine("| " + Md(lib.LibraryName) + status
                            + " | " + Md(ver)
                            + " | " + maven
                            + " | " + deps + " |");
            }
            sb.AppendLine();

            // Maven dependencies table
            if (mavenDeps.Count > 0)
            {
                sb.AppendLine("## Maven Dependencies");
                sb.AppendLine();
                sb.AppendLine("These are the underlying Java libraries identified via `<dependsOn>` metadata.");
                sb.AppendLine("Run OSV Scan to check these for known vulnerabilities.");
                sb.AppendLine();
                sb.AppendLine("| Group ID | Artifact ID | Version | PURL |");
                sb.AppendLine("|----------|-------------|---------|------|");

                // Sort by groupId then artifactId
                mavenDeps.Sort((a, b) =>
                {
                    int c = string.Compare(a.Maven.GroupId, b.Maven.GroupId, StringComparison.OrdinalIgnoreCase);
                    return c != 0 ? c : string.Compare(a.Maven.ArtifactId, b.Maven.ArtifactId, StringComparison.OrdinalIgnoreCase);
                });

                foreach (var dep in mavenDeps)
                {
                    sb.AppendLine("| `" + Md(dep.Maven.GroupId)    + "`"
                                + " | `" + Md(dep.Maven.ArtifactId) + "`"
                                + " | " + Md(dep.Maven.Version ?? "unknown")
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

        // Escape pipe characters so they don't break Markdown tables
        static string Md(string value)
        {
            if (value == null) return "";
            return value.Replace("|", "\\|").Replace("[", "\\[").Replace("]", "\\]");
        }
    }
}
