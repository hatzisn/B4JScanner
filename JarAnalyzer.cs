using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;

namespace B4JScanner
{
    static class JarAnalyzer
    {
        static readonly Regex _filenameVersionRe = new Regex(@"[-_](\d+[\.\d]*[\w\-]*)(?:\.jar)?$");

        public static LibraryInfo Analyze(ResolvedLibrary lib)
        {
            var info = new LibraryInfo { DisplayName = lib.LibraryName };

            // Extract Maven coords from the wrapper JAR (independent of version detection)
            if (lib.JarPath != null)
                info.Maven = TryReadMavenCoords(lib.JarPath);

            // 1. B4J XML descriptor
            if (lib.XmlPath != null)
            {
                TryReadXml(lib.XmlPath, info);
                if (!string.IsNullOrEmpty(info.Version))
                    return info;
            }

            // 2. JAR MANIFEST.MF
            if (lib.JarPath != null)
            {
                TryReadManifest(lib.JarPath, info);
                if (!string.IsNullOrEmpty(info.Version))
                    return info;
            }

            // 3. Version from JAR filename
            if (lib.JarPath != null)
            {
                string name = Path.GetFileNameWithoutExtension(lib.JarPath);
                var m = _filenameVersionRe.Match(name);
                if (m.Success)
                {
                    info.Version = m.Groups[1].Value;
                    info.VersionSource = "filename";
                    return info;
                }
            }

            // 4. Version embedded in library name (e.g. jserver-11.0.21)
            {
                var m = _filenameVersionRe.Match(lib.LibraryName);
                if (m.Success)
                {
                    info.Version = m.Groups[1].Value;
                    info.VersionSource = "library-name";
                    return info;
                }
            }

            info.Version = "unknown";
            info.VersionSource = "none";
            return info;
        }

        static void TryReadXml(string xmlPath, LibraryInfo info)
        {
            try
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var root = doc.DocumentElement;
                if (root == null) return;

                // <version> child element (not the doclet-version-NOT-library-version one)
                var verNode = root.SelectSingleNode("version");
                if (verNode != null && !string.IsNullOrEmpty(verNode.InnerText))
                {
                    info.Version = verNode.InnerText.Trim();
                    info.VersionSource = "xml";
                }

                // <dependsOn> child elements list dependency JAR names
                foreach (XmlNode dep in root.SelectNodes("dependsOn"))
                {
                    string val = dep.InnerText.Trim();
                    if (!string.IsNullOrEmpty(val))
                        info.Dependencies.Add(val);
                }
            }
            catch { }
        }

        public static MavenCoords TryReadMavenCoords(string jarPath)
        {
            try
            {
                using (var zip = ZipFile.OpenRead(jarPath))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (!entry.FullName.StartsWith("META-INF/maven/")) continue;
                        if (entry.Name != "pom.properties") continue;

                        using (var reader = new StreamReader(entry.Open()))
                        {
                            var props = new Dictionary<string, string>();
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                if (line.StartsWith("#")) continue;
                                int eq = line.IndexOf('=');
                                if (eq < 1) continue;
                                props[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                            }
                            string gId, aId, ver;
                            props.TryGetValue("groupId",    out gId);
                            props.TryGetValue("artifactId", out aId);
                            props.TryGetValue("version",    out ver);
                            if (!string.IsNullOrEmpty(gId) && !string.IsNullOrEmpty(aId))
                                return new MavenCoords { GroupId = gId, ArtifactId = aId, Version = ver };
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        static void TryReadManifest(string jarPath, LibraryInfo info)
        {
            try
            {
                using (var zip = ZipFile.OpenRead(jarPath))
                {
                    var entry = zip.GetEntry("META-INF/MANIFEST.MF");
                    if (entry == null) return;

                    using (var reader = new StreamReader(entry.Open()))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            int colon = line.IndexOf(':');
                            if (colon < 1) continue;
                            string key = line.Substring(0, colon).Trim();
                            string val = line.Substring(colon + 1).Trim();
                            if (string.IsNullOrEmpty(val)) continue;

                            switch (key)
                            {
                                case "Implementation-Version":
                                    if (string.IsNullOrEmpty(info.Version))
                                    { info.Version = val; info.VersionSource = "manifest:Implementation-Version"; }
                                    break;
                                case "Bundle-Version":
                                    if (string.IsNullOrEmpty(info.Version))
                                    { info.Version = val; info.VersionSource = "manifest:Bundle-Version"; }
                                    break;
                                case "Specification-Version":
                                    if (string.IsNullOrEmpty(info.Version))
                                    { info.Version = val; info.VersionSource = "manifest:Specification-Version"; }
                                    break;
                                case "Implementation-Vendor":
                                    if (string.IsNullOrEmpty(info.Vendor)) info.Vendor = val;
                                    break;
                                case "Implementation-Title":
                                    if (info.DisplayName == null || info.DisplayName == "") info.DisplayName = val;
                                    break;
                                case "Bundle-Description":
                                    if (string.IsNullOrEmpty(info.Description)) info.Description = val;
                                    break;
                                case "Bundle-Name":
                                    if (string.IsNullOrEmpty(info.DisplayName)) info.DisplayName = val;
                                    break;
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
