using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace B4JScanner
{
    static class OsvResultParser
    {
        public static List<OsvPackageResult> Parse(string json)
        {
            var list = new List<OsvPackageResult>();
            if (string.IsNullOrWhiteSpace(json)) return list;

            try
            {
                var jss = new JavaScriptSerializer();
                jss.MaxJsonLength = int.MaxValue;
                var root = jss.Deserialize<Dictionary<string, object>>(json);

                foreach (var resultItem in Seq(root, "results"))
                {
                    var result = resultItem as Dictionary<string, object>;
                    if (result == null) continue;

                    foreach (var pkgItem in Seq(result, "packages"))
                    {
                        var pkg = pkgItem as Dictionary<string, object>;
                        if (pkg == null) continue;

                        var pr = new OsvPackageResult();

                        var pkgInfo = Get(pkg, "package") as Dictionary<string, object>;
                        if (pkgInfo != null)
                        {
                            pr.PackageName = Get(pkgInfo, "name")      as string;
                            pr.Version     = Get(pkgInfo, "version")   as string;
                            pr.Ecosystem   = Get(pkgInfo, "ecosystem") as string;
                        }

                        foreach (var vItem in Seq(pkg, "vulnerabilities"))
                        {
                            var v = vItem as Dictionary<string, object>;
                            if (v == null) continue;

                            var ov = new OsvVuln();
                            ov.Id      = Get(v, "id")      as string;
                            ov.Summary = Get(v, "summary") as string;

                            foreach (object alias in Seq(v, "aliases"))
                                if (alias is string) ov.Aliases.Add((string)alias);

                            var dbSpec = Get(v, "database_specific") as Dictionary<string, object>;
                            if (dbSpec != null)
                                ov.Severity = Get(dbSpec, "severity") as string;

                            ov.FixedVersion = ExtractFixedVersion(v);

                            pr.Vulns.Add(ov);
                        }

                        if (pr.Vulns.Count > 0)
                            list.Add(pr);
                    }
                }
            }
            catch { }

            return list;
        }

        static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }

        static string ExtractFixedVersion(Dictionary<string, object> vuln)
        {
            foreach (var affectedItem in Seq(vuln, "affected"))
            {
                var affected = affectedItem as Dictionary<string, object>;
                if (affected == null) continue;

                foreach (var rangeItem in Seq(affected, "ranges"))
                {
                    var range = rangeItem as Dictionary<string, object>;
                    if (range == null) continue;

                    // Only look at ECOSYSTEM ranges — these carry the package version
                    string rangeType = Get(range, "type") as string;
                    if (!string.Equals(rangeType, "ECOSYSTEM", StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (var eventItem in Seq(range, "events"))
                    {
                        var ev = eventItem as Dictionary<string, object>;
                        if (ev == null) continue;
                        string fixed_ = Get(ev, "fixed") as string;
                        if (!string.IsNullOrEmpty(fixed_))
                            return fixed_;
                    }
                }
            }
            return null;
        }

        // Handles both object[] and ArrayList (JavaScriptSerializer can return either)
        static IEnumerable Seq(Dictionary<string, object> d, string key)
        {
            var v = Get(d, key);
            if (v is IEnumerable && !(v is string)) return (IEnumerable)v;
            return new object[0];
        }
    }
}
