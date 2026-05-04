using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace B4JScanner
{
    static class B4JProjectParser
    {
        static readonly Regex _libraryRe      = new Regex(@"^Library\d+=(.+)$",      RegexOptions.IgnoreCase);
        static readonly Regex _moduleRe       = new Regex(@"^Module\d+=(.+?)(\||$)", RegexOptions.IgnoreCase);
        static readonly Regex _build1Re       = new Regex(@"^Build1=\S+?,(.+)$",     RegexOptions.IgnoreCase);
        static readonly Regex _versionRe      = new Regex(@"^Version=(.+)$",         RegexOptions.IgnoreCase);
        static readonly Regex _additionalJarRe = new Regex(@"^\s*#AdditionalJar:\s*(.+?)\s*$", RegexOptions.IgnoreCase);

        public static B4JProject Parse(string path)
        {
            string projectFile;
            string projectFolder;

            if (Directory.Exists(path))
            {
                projectFolder = path;
                var b4jFiles = Directory.GetFiles(path, "*.b4j");
                if (b4jFiles.Length == 0)
                    throw new Exception("No .b4j file found in: " + path);
                projectFile = b4jFiles[0];
            }
            else if (File.Exists(path))
            {
                projectFile = path;
                projectFolder = Path.GetDirectoryName(path);
            }
            else
            {
                throw new Exception("Path not found: " + path);
            }

            var project = new B4JProject
            {
                ProjectFile   = projectFile,
                ProjectFolder = projectFolder,
                Name          = Path.GetFileNameWithoutExtension(projectFile)
            };

            // Track seen jar names across all files to avoid duplicates
            var seenJars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Read the .b4j file: header section up to @EndOfDesignText@, then code section
            bool inCode = false;
            foreach (var line in File.ReadLines(projectFile))
            {
                if (!inCode)
                {
                    if (line == "@EndOfDesignText@") { inCode = true; continue; }

                    Match m;
                    m = _libraryRe.Match(line);
                    if (m.Success) { project.Libraries.Add(m.Groups[1].Value.Trim()); continue; }

                    m = _moduleRe.Match(line);
                    if (m.Success) { project.Modules.Add(m.Groups[1].Value.Trim()); continue; }

                    m = _build1Re.Match(line);
                    if (m.Success) { project.JavaPackage = m.Groups[1].Value.Trim(); continue; }

                    m = _versionRe.Match(line);
                    if (m.Success) { project.Version = m.Groups[1].Value.Trim(); continue; }
                }
                else
                {
                    CollectAdditionalJar(line, project, seenJars);
                }
            }

            // Scan all .bas module files in the project folder
            try
            {
                foreach (var basFile in Directory.GetFiles(projectFolder, "*.bas", SearchOption.AllDirectories))
                {
                    foreach (var line in File.ReadLines(basFile))
                        CollectAdditionalJar(line, project, seenJars);
                }
            }
            catch { }

            return project;
        }

        static void CollectAdditionalJar(string line, B4JProject project, HashSet<string> seen)
        {
            var m = _additionalJarRe.Match(line);
            if (!m.Success) return;
            string jar = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(jar) && seen.Add(jar))
                project.AdditionalJars.Add(jar);
        }
    }
}
