using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace B4JScanner
{
    static class JavaSourceScanner
    {
        static readonly Regex _packageRe = new Regex(@"^\s*package\s+([\w\.]+)\s*;");
        static readonly Regex _importRe  = new Regex(@"^\s*import\s+([\w\.]+(?:\.\*)?)\s*;");

        public static List<JavaSourceFile> Scan(string projectFolder)
        {
            var results = new List<JavaSourceFile>();
            string srcRoot = Path.Combine(projectFolder, "Objects", "src");
            if (!Directory.Exists(srcRoot))
                return results;

            foreach (var javaFile in Directory.GetFiles(srcRoot, "*.java", SearchOption.AllDirectories))
            {
                var jsf = new JavaSourceFile
                {
                    FilePath = javaFile,
                    ClassName = Path.GetFileNameWithoutExtension(javaFile)
                };

                try
                {
                    foreach (var line in File.ReadLines(javaFile))
                    {
                        var pm = _packageRe.Match(line);
                        if (pm.Success && jsf.Package == null)
                        {
                            jsf.Package = pm.Groups[1].Value;
                            continue;
                        }

                        var im = _importRe.Match(line);
                        if (im.Success)
                        {
                            string imp = im.Groups[1].Value;
                            // Filter out B4J framework and java.* / javax.*
                            if (!imp.StartsWith("anywheresoftware.", StringComparison.OrdinalIgnoreCase) &&
                                !imp.StartsWith("java.", StringComparison.OrdinalIgnoreCase) &&
                                !imp.StartsWith("javax.", StringComparison.OrdinalIgnoreCase) &&
                                !imp.StartsWith("android.", StringComparison.OrdinalIgnoreCase))
                            {
                                jsf.Imports.Add(imp);
                            }
                        }
                    }
                }
                catch { }

                results.Add(jsf);
            }

            return results;
        }

        // Return unique top-level package prefixes from all imports (e.g. "com.zaxxer", "org.eclipse")
        public static List<string> GetUniquePackagePrefixes(List<JavaSourceFile> files)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();

            foreach (var f in files)
            {
                foreach (var imp in f.Imports)
                {
                    string[] parts = imp.Split('.');
                    if (parts.Length >= 2)
                    {
                        string prefix = parts[0] + "." + parts[1];
                        if (seen.Add(prefix))
                            result.Add(prefix);
                    }
                }
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }
    }
}
