using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace B4JScanner
{
    class AppConfig
    {
        public string ProjectFolder { get; set; }
        public string LibrariesPath { get; set; }
        public string AdditionalLibrariesPath { get; set; }
        public bool? MavenSearchEnabled { get; set; }

        static string ConfigPath
        {
            get
            {
                return Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "b4jscanner.cfg.json");
            }
        }

        public static AppConfig Load()
        {
            var cfg = new AppConfig
            {
                LibrariesPath           = @"C:\Apps\B4J\Libraries",
                AdditionalLibrariesPath = @"C:\Apps\B4J\AdditionalLibraries"
            };

            string path = ConfigPath;
            if (!File.Exists(path)) return cfg;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);

                string pf = ReadString(json, "projectFolder");
                if (pf != null) cfg.ProjectFolder = pf;

                string libs = ReadString(json, "librariesPath");
                if (libs != null) cfg.LibrariesPath = libs;

                string addLibs = ReadString(json, "additionalLibrariesPath");
                if (addLibs != null) cfg.AdditionalLibrariesPath = addLibs;

                bool? maven = ReadBool(json, "mavenSearchEnabled");
                if (maven.HasValue) cfg.MavenSearchEnabled = maven;
            }
            catch { }

            return cfg;
        }

        public void Save()
        {
            string mavenVal = MavenSearchEnabled.HasValue
                ? (MavenSearchEnabled.Value ? "true" : "false")
                : "null";

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"projectFolder\": "           + Str(ProjectFolder)           + ",");
            sb.AppendLine("  \"librariesPath\": "           + Str(LibrariesPath)           + ",");
            sb.AppendLine("  \"additionalLibrariesPath\": " + Str(AdditionalLibrariesPath) + ",");
            sb.AppendLine("  \"mavenSearchEnabled\": "      + mavenVal);
            sb.AppendLine("}");
            File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
        }

        static string ReadString(string json, string key)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success) return null;
            return m.Groups[1].Value
                .Replace("\\\\", "\x01")
                .Replace("\\\"", "\"")
                .Replace("\\n",  "\n")
                .Replace("\\r",  "\r")
                .Replace("\\t",  "\t")
                .Replace("\x01", "\\");
        }

        static bool? ReadBool(string json, string key)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false|null)");
            if (!m.Success) return null;
            string val = m.Groups[1].Value;
            if (val == "true")  return true;
            if (val == "false") return false;
            return null;
        }

        static string Str(string value)
        {
            if (value == null) return "null";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
