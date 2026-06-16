using AdminSite.Models;
using System.Text;

namespace AdminSite.Services
{
    public static class AdminAppearancePresets
    {
        public static IReadOnlyList<AdminAppearancePresetModel> All { get; } = new List<AdminAppearancePresetModel>
        {
            new()
            {
                Key = "navy-gold",
                Name = "Navy Gold",
                Description = "Deep navy workspace with a warm gold accent.",
                Sidebar = "#061b3a",
                Primary = "#0b2a55",
                Accent = "#c9a24d",
                Background = "#f4f7fb",
                Surface = "#ffffff",
                Text = "#102033",
                Muted = "#667085",
                Border = "#d8e2ef",
                Danger = "#c2413b",
                Success = "#2f855a"
            },
            new()
            {
                Key = "granite",
                Name = "Granite",
                Description = "Faded graphite with a restrained steel accent.",
                Sidebar = "#2f3338",
                Primary = "#3f454d",
                Accent = "#7b8fa6",
                Background = "#f3f4f6",
                Surface = "#ffffff",
                Text = "#1f2933",
                Muted = "#6b7280",
                Border = "#d7dce2",
                Danger = "#b94a48",
                Success = "#3d7a5f"
            },
            new()
            {
                Key = "cloud-mono",
                Name = "Cloud Mono",
                Description = "Black, white, and grey with a clean editorial feel.",
                Sidebar = "#111827",
                Primary = "#1f2937",
                Accent = "#8b949e",
                Background = "#f7f8fa",
                Surface = "#ffffff",
                Text = "#111827",
                Muted = "#6b7280",
                Border = "#d1d5db",
                Danger = "#b91c1c",
                Success = "#166534"
            },
            new()
            {
                Key = "harbor-teal",
                Name = "Harbor Teal",
                Description = "Deep teal-blue with amber highlights.",
                Sidebar = "#083f49",
                Primary = "#0f6674",
                Accent = "#d99a2b",
                Background = "#eef7f8",
                Surface = "#ffffff",
                Text = "#102a30",
                Muted = "#60747a",
                Border = "#c9dee2",
                Danger = "#bd3b3b",
                Success = "#2f7d62"
            },
            new()
            {
                Key = "burgundy-ivory",
                Name = "Burgundy Ivory",
                Description = "Formal burgundy with ivory surfaces and antique gold.",
                Sidebar = "#4a1424",
                Primary = "#6d1f35",
                Accent = "#b88a44",
                Background = "#fbf7f0",
                Surface = "#fffdf8",
                Text = "#2d1d20",
                Muted = "#78696b",
                Border = "#e4d7c6",
                Danger = "#a73535",
                Success = "#3f7a4f"
            }
        };

        public static AdminAppearancePresetModel Get(string? key) =>
            All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)) ?? All[0];

        public static string ToCssVariables(string? key)
        {
            var preset = Get(key);
            var hover = Darken(preset.Primary, 0.12);
            var light = Lighten(preset.Primary, 0.18);

            var sb = new StringBuilder();
            sb.Append("--primary:").Append(preset.Primary).Append(';');
            sb.Append("--primary-light:").Append(light).Append(';');
            sb.Append("--primary-hover:").Append(hover).Append(';');
            sb.Append("--sidebar-bg:").Append(preset.Sidebar).Append(';');
            sb.Append("--sidebar-text:#ffffff;");
            sb.Append("--accent:").Append(preset.Accent).Append(';');
            sb.Append("--bg:").Append(preset.Background).Append(';');
            sb.Append("--surface:").Append(preset.Surface).Append(';');
            sb.Append("--border:").Append(preset.Border).Append(';');
            sb.Append("--text:").Append(preset.Text).Append(';');
            sb.Append("--text-muted:").Append(preset.Muted).Append(';');
            sb.Append("--danger:").Append(preset.Danger).Append(';');
            sb.Append("--success:").Append(preset.Success).Append(';');
            return sb.ToString();
        }

        private static string Darken(string hex, double amount) => Shift(hex, -amount);
        private static string Lighten(string hex, double amount) => Shift(hex, amount);

        private static string Shift(string hex, double amount)
        {
            hex = hex.Trim().TrimStart('#');
            if (hex.Length != 6) return "#0b2a55";

            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);

            int channel(int value) => amount >= 0
                ? (int)Math.Round(value + (255 - value) * amount)
                : (int)Math.Round(value * (1 + amount));

            return $"#{channel(r):X2}{channel(g):X2}{channel(b):X2}";
        }
    }
}
