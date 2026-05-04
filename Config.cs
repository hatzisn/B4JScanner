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
            }
            catch { }

            return cfg;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"projectFolder\": "           + Str(ProjectFolder)           + ",");
            sb.AppendLine("  \"librariesPath\": "           + Str(LibrariesPath)           + ",");
            sb.AppendLine("  \"additionalLibrariesPath\": " + Str(AdditionalLibrariesPath));
            sb.AppendLine("}");
            File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
        }

        // Reads a JSON string value by key using a simple regex (no parser needed for this flat config)
        static string ReadString(string json, string key)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success) return null;
            // Unescape common sequences
            return m.Groups[1].Value
                .Replace("\\\\", "\x01")   // temp-protect \\
                .Replace("\\\"", "\"")
                .Replace("\\n",  "\n")
                .Replace("\\r",  "\r")
                .Replace("\\t",  "\t")
                .Replace("\x01", "\\");
        }

        static string Str(string value)
        {
            if (value == null) return "null";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
