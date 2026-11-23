using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
using DiscordRPC;

namespace GuideStrap
{
    // Translations: English text is the key. Text with {0}, {1}... is matched as a template.
    static class L
    {
        public static readonly (string Code, string Native)[] Languages =
        {
            ("en", "English"), ("es", "Español"), ("pt", "Português"),
            ("fr", "Français"), ("de", "Deutsch"), ("tr", "Türkçe"), ("fil", "Filipino"),
        };
        static string lang = "en";
        static Dictionary<string, string>? table;
        static readonly List<(Regex Pattern, string Translation)> templates = new();

        public static string Lang
        {
            get => lang;
            set
            {
                string v = Languages.Any(x => x.Code == value) ? value : "en";
                if (v == lang) return;
                lang = v;
                table = null;
                templates.Clear();
            }
        }

        public static string SystemLanguage()
        {
            string c = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (c == "tl") c = "fil";
            return Languages.Any(x => x.Code == c) ? c : "en";
        }

        static void Load()
        {
            if (table != null) return;
            table = Translations.For(lang);
            foreach (var (key, value) in table.Where(kv => kv.Key.Contains("{0}")).OrderByDescending(kv => kv.Key.Length))
            {
                string pattern = "^" + Regex.Replace(Regex.Escape(key), @"\\\{(\d+)}", m => $"(?<g{m.Groups[1].Value}>.*?)") + "$";
                templates.Add((new Regex(pattern, RegexOptions.Singleline), value));
            }
        }

        public static string T(string text)
        {
            if (lang == "en" || string.IsNullOrEmpty(text)) return text;
            Load();
            if (table!.TryGetValue(text, out var hit)) return hit;
            foreach (var (pattern, translation) in templates)
            {
                var m = pattern.Match(text);
                if (m.Success) return Regex.Replace(translation, @"\{(\d+)\}", g => m.Groups["g" + g.Groups[1].Value].Value);
            }
            return text;
        }

        public static string F(string template, params object[] args) => string.Format(T(template), args);

        // Translates text that was set directly on controls (labels, buttons, placeholders).
        public static void Apply(Control root)
        {
            if (lang == "en") return;
            switch (root)
            {
                case TextBox tb: tb.PlaceholderText = T(tb.PlaceholderText); break;
                case Label or RoundButton or LauncherButton or LinkText: root.Text = T(root.Text); break;
            }
            foreach (Control child in root.Controls) Apply(child);
        }
    }
}
