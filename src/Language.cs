using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PersonalStickyNote
{
    internal static class L
    {
        // These columns match Strings.tsv. The first column is the Chinese text, with an optional |context.
        public static readonly string[] Codes = { "zh", "en", "fr", "es", "ja", "ko" };
        public static readonly string[] Names = { "简体中文", "English", "Français", "Español", "日本語", "한국어" };
        public const string SystemLanguage = "system";
        private static readonly Dictionary<string, string[]> Strings = Load();
        private static volatile int current = Resolve(SystemLanguage);

        public static string Normalize(string preference)
        {
            return Array.IndexOf(Codes, preference) >= 0 ? preference : SystemLanguage;
        }

        private static int Resolve(string preference)
        {
            var code = Normalize(preference);
            if (code == SystemLanguage) code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            int index = Array.IndexOf(Codes, code);
            return index < 0 ? 1 : index;
        }

        public static void Use(string preference) { current = Resolve(preference); }

        public static string T(string key)
        {
            string[] row;
            return Strings.TryGetValue(key, out row) ? row[current] : key;
        }

        public static string T(string key, params object[] arguments)
        {
            return string.Format(T(key), arguments);
        }

        public static bool IsUngroupedName(string value)
        {
            return Array.Exists(Strings["未分组"], name => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
        }

        private static Dictionary<string, string[]> Load()
        {
            var strings = new Dictionary<string, string[]>(StringComparer.Ordinal);
            using (var reader = new StreamReader(typeof(L).Assembly.GetManifestResourceStream("PersonalStickyNote.Strings.tsv")))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    var row = line.Split('\t');
                    if (row.Length != Codes.Length) throw new InvalidDataException("Invalid translation row: " + line);
                    for (int i = 0; i < row.Length; i++) row[i] = row[i].Replace(@"\n", "\n");
                    string key = row[0];
                    int context = key.IndexOf('|');
                    if (context >= 0) row[0] = key.Substring(0, context);
                    strings.Add(key, row);
                }
            }
            return strings;
        }
    }
}
