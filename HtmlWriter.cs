using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace B4JScanner
{
    static class HtmlWriter
    {
        const string ToolVersion = "1.0";

        public static string Write(B4JProject project, List<ResolvedLibrary> libraries,
            List<JavaSourceFile> javaFiles, string outputPath,
            List<OsvPackageResult> osvResults = null)
        {
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

            int totalVulns = 0;
            string worstSev = null;
            if (osvResults != null)
            {
                foreach (var p in osvResults)
                {
                    totalVulns += p.Vulns.Count;
                    foreach (var v in p.Vulns)
                        worstSev = WorstSev(worstSev, v.Severity);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            sb.AppendLine("<title>SBOM: " + H(project.Name) + "</title>");
            sb.AppendLine("<style>");
            sb.Append(Css());
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class=\"wrap\">");

            // Header
            sb.AppendLine("<header>");
            sb.AppendLine("<h1>SBOM Report: " + H(project.Name) + "</h1>");
            sb.AppendLine("<div class=\"meta\">Generated " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")
                + " UTC &nbsp;&middot;&nbsp; B4JScanner v" + ToolVersion + "</div>");
            sb.AppendLine("</header>");

            // Summary cards
            sb.AppendLine("<div class=\"cards\">");
            sb.AppendLine(Card("blue",  libraries.Count.ToString(), "Libraries"));
            sb.AppendLine(Card("green", found.ToString(),           "Found"));
            if (notFound > 0)
                sb.AppendLine(Card("red", notFound.ToString(), "Not Found"));
            sb.AppendLine(Card("blue", mavenDeps.Count.ToString(), "Maven Deps"));
            if (osvResults == null)
                sb.AppendLine(Card("gray",  "?",                    "Vulnerabilities"));
            else if (totalVulns == 0)
                sb.AppendLine(Card("green", "0",                    "Vulnerabilities"));
            else
                sb.AppendLine(Card(SevCardColor(worstSev), totalVulns.ToString(), "Vulnerabilities"));
            sb.AppendLine("</div>");

            // Project info
            sb.AppendLine("<h2>Project</h2>");
            sb.AppendLine("<table><tbody>");
            sb.AppendLine(InfoRow("Name",    project.Name));
            sb.AppendLine(InfoRow("Version", project.Version ?? "unknown"));
            if (!string.IsNullOrEmpty(project.JavaPackage))
                sb.AppendLine(InfoRow("Package", project.JavaPackage));
            sb.AppendLine(InfoRow("B4J File", project.ProjectFile));
            sb.AppendLine("</tbody></table>");

            // Libraries
            sb.AppendLine("<h2>B4J Libraries</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>"
                + "<th>Library</th><th>Version</th><th>Type</th>"
                + "<th>Maven Coords</th><th style=\"text-align:right\">Deps</th><th>Status</th>"
                + "</tr></thead>");
            sb.AppendLine("<tbody>");
            foreach (var lib in libraries)
            {
                var info = lib.Info;
                string ver   = info != null && !string.IsNullOrEmpty(info.Version) ? info.Version : "unknown";
                string maven = info != null && info.Maven != null
                    ? "<code>" + H(info.Maven.GroupId) + ":" + H(info.Maven.ArtifactId) + "</code>"
                    : "<span class=\"dim\">-</span>";
                int depCount  = info != null ? info.ResolvedDeps.Count : 0;
                string typeTag = lib.IsAdditionalJar
                    ? "<span class=\"bge aj\">AJ</span>"
                    : "<span class=\"dim\">B4J</span>";
                string statusTag = lib.Found
                    ? "<span class=\"bge ok\">Found</span>"
                    : "<span class=\"bge miss\">Missing</span>";

                sb.AppendLine("<tr>"
                    + "<td>" + H(lib.LibraryName) + "</td>"
                    + "<td><code>" + H(ver) + "</code></td>"
                    + "<td>" + typeTag + "</td>"
                    + "<td>" + maven + "</td>"
                    + "<td style=\"text-align:right\">" + (depCount > 0 ? depCount.ToString() : "-") + "</td>"
                    + "<td>" + statusTag + "</td>"
                    + "</tr>");
            }
            sb.AppendLine("</tbody></table>");

            // Maven dependencies
            if (mavenDeps.Count > 0)
            {
                mavenDeps.Sort((a, b) =>
                {
                    int c = string.Compare(a.Maven.GroupId, b.Maven.GroupId, StringComparison.OrdinalIgnoreCase);
                    return c != 0 ? c : string.Compare(a.Maven.ArtifactId, b.Maven.ArtifactId, StringComparison.OrdinalIgnoreCase);
                });

                sb.AppendLine("<h2>Maven Dependencies</h2>");
                sb.AppendLine("<table>");
                sb.AppendLine("<thead><tr><th>Group ID</th><th>Artifact ID</th><th>Version</th><th>PURL</th></tr></thead>");
                sb.AppendLine("<tbody>");
                foreach (var dep in mavenDeps)
                {
                    sb.AppendLine("<tr>"
                        + "<td><code>" + H(dep.Maven.GroupId)             + "</code></td>"
                        + "<td><code>" + H(dep.Maven.ArtifactId)          + "</code></td>"
                        + "<td><code>" + H(dep.Maven.Version ?? "unknown") + "</code></td>"
                        + "<td><code class=\"purl\">" + H(dep.Maven.ToPurl()) + "</code></td>"
                        + "</tr>");
                }
                sb.AppendLine("</tbody></table>");
            }

            // Vulnerabilities
            sb.AppendLine("<h2>Vulnerabilities</h2>");
            if (osvResults == null)
            {
                sb.AppendLine("<div class=\"notice not-scanned\">"
                    + "OSV Scan has not been run. Click <strong>OSV Scan</strong> to check for known vulnerabilities."
                    + "</div>");
            }
            else if (totalVulns == 0)
            {
                sb.AppendLine("<div class=\"notice none-found\">&#10003;&nbsp; No known vulnerabilities found.</div>");
            }
            else
            {
                foreach (var pkg in osvResults)
                {
                    if (pkg.Vulns.Count == 0) continue;
                    sb.AppendLine("<table style=\"margin-bottom:14px\">");
                    sb.AppendLine("<thead><tr><th colspan=\"4\">"
                        + H(pkg.PackageName)
                        + "&nbsp;<code>" + H(pkg.Version ?? "") + "</code>"
                        + "</th></tr>"
                        + "<tr><th>ID</th><th>Aliases</th><th>Severity</th><th>Summary</th></tr>"
                        + "</thead><tbody>");
                    foreach (var v in pkg.Vulns)
                    {
                        string sevBadge = "<span class=\"bge " + SevClass(v.Severity) + "\">"
                            + H(v.Severity ?? "unknown") + "</span>";
                        string aliases = v.Aliases.Count > 0
                            ? "<code>" + H(string.Join(", ", v.Aliases.ToArray())) + "</code>"
                            : "<span class=\"dim\">-</span>";
                        sb.AppendLine("<tr>"
                            + "<td><code>" + H(v.Id) + "</code></td>"
                            + "<td>" + aliases + "</td>"
                            + "<td>" + sevBadge + "</td>"
                            + "<td>" + H(v.Summary ?? "") + "</td>"
                            + "</tr>");
                    }
                    sb.AppendLine("</tbody></table>");
                }
            }

            // Java import prefixes
            var prefixes = JavaSourceScanner.GetUniquePackagePrefixes(javaFiles);
            if (prefixes.Count > 0)
            {
                sb.AppendLine("<h2>Java Import Prefixes</h2>");
                sb.AppendLine("<p class=\"section-note\">Third-party package prefixes from generated <code>Objects/src</code> Java files.</p>");
                sb.AppendLine("<div class=\"prefix-list\">");
                foreach (var p in prefixes)
                    sb.AppendLine("<code class=\"prefix-tag\">" + H(p) + "</code>");
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</div>"); // .wrap
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
            return outputPath;
        }

        // --- Helpers ---

        static string Card(string colorClass, string val, string label)
        {
            return "<div class=\"card " + colorClass + "\">"
                 + "<div class=\"val\">" + val + "</div>"
                 + "<div class=\"lbl\">" + H(label) + "</div>"
                 + "</div>";
        }

        static string InfoRow(string label, string value)
        {
            return "<tr>"
                 + "<th>" + H(label) + "</th>"
                 + "<td><code>" + H(value ?? "") + "</code></td>"
                 + "</tr>";
        }

        static string SevClass(string sev)
        {
            if (string.IsNullOrEmpty(sev)) return "unknown";
            switch (sev.ToUpperInvariant())
            {
                case "CRITICAL": return "critical";
                case "HIGH":     return "high";
                case "MEDIUM":   return "medium";
                case "LOW":      return "low";
                default:         return "unknown";
            }
        }

        static string SevCardColor(string sev)
        {
            if (string.IsNullOrEmpty(sev)) return "amber";
            switch (sev.ToUpperInvariant())
            {
                case "CRITICAL": return "purple";
                case "HIGH":     return "red";
                case "MEDIUM":   return "amber";
                case "LOW":      return "green";
                default:         return "amber";
            }
        }

        static string WorstSev(string current, string candidate)
        {
            return SevRank(candidate) > SevRank(current) ? candidate : current;
        }

        static int SevRank(string s)
        {
            if (s == null) return 0;
            switch (s.ToUpperInvariant())
            {
                case "CRITICAL": return 4;
                case "HIGH":     return 3;
                case "MEDIUM":   return 2;
                case "LOW":      return 1;
                default:         return 0;
            }
        }

        static string H(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;");
        }

        static string Css()
        {
            return
@"*{box-sizing:border-box;margin:0;padding:0}
body{font:14px/1.6 system-ui,-apple-system,BlinkMacSystemFont,sans-serif;background:#f1f5f9;color:#1e293b}
.wrap{max-width:1100px;margin:0 auto;padding:24px}
header{background:linear-gradient(135deg,#1e3a8a,#2563eb);color:#fff;padding:20px 28px;border-radius:10px;margin-bottom:20px}
header h1{font-size:22px;font-weight:700;letter-spacing:-.3px}
header .meta{font-size:12px;opacity:.8;margin-top:5px}
h2{font-size:13px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;margin:28px 0 10px;padding-bottom:7px;border-bottom:2px solid #2563eb;color:#0f172a}
.cards{display:flex;flex-wrap:wrap;gap:10px;margin-bottom:6px}
.card{background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:14px 18px;min-width:130px;border-top:3px solid #94a3b8}
.card.blue{border-top-color:#2563eb}.card.green{border-top-color:#16a34a}
.card.red{border-top-color:#dc2626}.card.amber{border-top-color:#d97706}
.card.purple{border-top-color:#7c3aed}.card.gray{border-top-color:#94a3b8}
.card .val{font-size:26px;font-weight:800;color:#0f172a;line-height:1}
.card .lbl{font-size:11px;color:#64748b;text-transform:uppercase;letter-spacing:.05em;margin-top:5px}
table{width:100%;border-collapse:collapse;background:#fff;border-radius:8px;overflow:hidden;border:1px solid #e2e8f0;font-size:13px}
thead th{background:#f8fafc;padding:8px 12px;text-align:left;font-weight:600;color:#475569;border-bottom:1px solid #e2e8f0;font-size:11px;text-transform:uppercase;letter-spacing:.05em}
tbody th{background:#f8fafc;padding:8px 12px;text-align:left;font-weight:600;color:#475569;width:130px;border-bottom:1px solid #f1f5f9;font-size:13px;text-transform:none;letter-spacing:0}
td{padding:8px 12px;border-bottom:1px solid #f1f5f9;vertical-align:middle}
tr:last-child td,tr:last-child th{border-bottom:none}
.bge{display:inline-block;padding:2px 9px;border-radius:99px;font-size:11px;font-weight:700;letter-spacing:.02em;white-space:nowrap}
.bge.ok{background:#dcfce7;color:#166534}
.bge.miss{background:#fee2e2;color:#991b1b}
.bge.aj{background:#dbeafe;color:#1e40af}
.bge.critical{background:#ede9fe;color:#5b21b6}
.bge.high{background:#fee2e2;color:#991b1b}
.bge.medium{background:#fef3c7;color:#92400e}
.bge.low{background:#d1fae5;color:#065f46}
.bge.unknown{background:#f1f5f9;color:#64748b}
code{background:#f1f5f9;padding:1px 6px;border-radius:3px;font-family:ui-monospace,Consolas,monospace;font-size:12px;color:#0f172a}
code.purl{font-size:11px;color:#475569;word-break:break-all}
.dim{color:#94a3b8}
.notice{padding:13px 18px;border-radius:8px;font-size:13px;border:1px solid}
.notice.none-found{background:#f0fdf4;border-color:#86efac;color:#166534}
.notice.not-scanned{background:#f8fafc;border-color:#cbd5e1;color:#64748b}
.section-note{font-size:12px;color:#64748b;margin-bottom:10px}
.prefix-list{display:flex;flex-wrap:wrap;gap:6px}
.prefix-tag{background:#f1f5f9;padding:4px 10px;border-radius:5px;border:1px solid #e2e8f0;font-size:12px}
";
        }
    }
}
