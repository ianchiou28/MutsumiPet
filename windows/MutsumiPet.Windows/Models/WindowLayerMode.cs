namespace MutsumiPet.Models
{
    public enum WindowLayerMode
    {
        Front,
        Normal,
        Desktop
    }

    public static class WindowLayerModes
    {
        public static readonly WindowLayerMode[] AllCases =
        {
            WindowLayerMode.Front,
            WindowLayerMode.Normal,
            WindowLayerMode.Desktop
        };

        public static string Title(WindowLayerMode mode)
        {
            switch (mode)
            {
                case WindowLayerMode.Normal: return "普通窗口";
                case WindowLayerMode.Desktop: return "位于桌面后台";
                default: return "始终置顶";
            }
        }

        /// Stable string used for persistence, matching the macOS build's raw values.
        public static string RawValue(WindowLayerMode mode)
        {
            switch (mode)
            {
                case WindowLayerMode.Normal: return "normal";
                case WindowLayerMode.Desktop: return "desktop";
                default: return "front";
            }
        }

        public static WindowLayerMode Parse(string rawValue, WindowLayerMode fallback)
        {
            switch (rawValue)
            {
                case "front": return WindowLayerMode.Front;
                case "normal": return WindowLayerMode.Normal;
                case "desktop": return WindowLayerMode.Desktop;
                default: return fallback;
            }
        }
    }
}
