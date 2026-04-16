using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using MelonLoader;
using UnityEngine;

namespace DataCenterCrosshair;

/// <summary>Loads size and hex color from <c>DataCenterCrosshair.txt</c> in the game install folder (same folder as the game exe).</summary>
internal static class CrosshairSettings
{
    internal const float SizeMin = 2f;
    internal const float SizeMax = 128f;

    private const string ConfigFileName = "DataCenterCrosshair.txt";

    private static readonly string ConfigPath = Path.Combine(ResolveGameRootDirectory(), ConfigFileName);

    private static float _size = 12f;
    private static string _colorHex = "#FFFFFFFF";
    private static long _lastWriteTicks = long.MinValue;

    private static bool _pointerTextEnabled = true;
    private static float _pointerTextFontSize = 22f;
    private static string _pointerTextColorHex = "#FFFF88FF";
    private static float _pointerTextOffsetX;
    private static float _pointerTextOffsetY = 48f;

    private static bool _pointerTextBackgroundEnabled = true;
    private static string _pointerTextBackgroundColorHex = "#000000B0";
    private static float _pointerTextBackgroundPadX = 8f;
    private static float _pointerTextBackgroundPadY = 4f;

    private static float _pointerBackgroundLiftX;
    private static float _pointerBackgroundLiftY;

    internal static float Size => Mathf.Clamp(_size, SizeMin, SizeMax);

    internal static string ColorHexRaw => _colorHex;

    internal static bool PointerTextEnabled => _pointerTextEnabled;

    internal static float PointerTextFontSize => Mathf.Clamp(_pointerTextFontSize, 4f, 200f);

    internal static string PointerTextColorHexRaw => _pointerTextColorHex;

    internal static float PointerTextOffsetX => Mathf.Clamp(_pointerTextOffsetX, -600f, 600f);

    internal static float PointerTextOffsetY => Mathf.Clamp(_pointerTextOffsetY, -600f, 600f);

    internal static bool PointerTextBackgroundEnabled => _pointerTextBackgroundEnabled;

    internal static string PointerTextBackgroundColorHexRaw => _pointerTextBackgroundColorHex;

    internal static float PointerTextBackgroundPadX => Mathf.Clamp(_pointerTextBackgroundPadX, 0f, 80f);

    internal static float PointerTextBackgroundPadY => Mathf.Clamp(_pointerTextBackgroundPadY, 0f, 80f);

    /// <summary>Extra local X on the background panel only (pixels).</summary>
    internal static float PointerBackgroundLiftX => Mathf.Clamp(_pointerBackgroundLiftX, -120f, 120f);

    /// <summary>Extra local Y on the background panel only (pixels, positive nudges panel up over the text).</summary>
    internal static float PointerBackgroundLiftY => Mathf.Clamp(_pointerBackgroundLiftY, -120f, 120f);

    /// <summary>Avoid referencing obsolete <c>MelonUtils.GameDirectory</c> when the SDK treats CS0619 as an error.</summary>
    private static string ResolveGameRootDirectory()
    {
        try
        {
            var envType = Type.GetType("MelonLoader.MelonEnvironment, MelonLoader");
            var p = envType?.GetProperty("GameRootDirectory", BindingFlags.Public | BindingFlags.Static);
            var s = p?.GetValue(null) as string;
            if (!string.IsNullOrEmpty(s) && Directory.Exists(s))
                return s;
        }
        catch
        {
            // ignored
        }

        try
        {
            var utilsType = Type.GetType("MelonLoader.MelonUtils, MelonLoader");
            var p = utilsType?.GetProperty("GameDirectory", BindingFlags.Public | BindingFlags.Static);
            var s = p?.GetValue(null) as string;
            if (!string.IsNullOrEmpty(s) && Directory.Exists(s))
                return s;
        }
        catch
        {
            // ignored
        }

        return ".";
    }

    private static string DefaultFileContents =>
        new StringBuilder()
            .AppendLine("// Data Center Crosshair — edit and save; changes apply within about one second.")
            .AppendLine("// Lines starting with // are comments.")
            .AppendLine("// size = dot width/height in UI pixels (clamped between 2 and 128).")
            .AppendLine("// color = HTML hex: #RRGGBB or #RRGGBBAA (red, green, blue, optional alpha).")
            .AppendLine("// --- Bandwidth / label under crosshair (e.g. \"0 / 10 Gbps\") ---")
            .AppendLine("// pointerTextEnabled = true or false")
            .AppendLine("// pointerTextFontSize = TMP font size (4–200)")
            .AppendLine("// pointerTextColor = hex (same rules as color=)")
            .AppendLine("// pointerTextOffsetY / pointerTextOffsetX = same as pointerLabelLiftY / pointerLabelLiftX (legacy names)")
            .AppendLine("// pointerLabelLiftY = move label + background together (positive = higher on screen for typical UI anchors)")
            .AppendLine("// pointerLabelLiftX = move label + background sideways (positive = right)")
            .AppendLine("// pointerBackgroundLiftY / pointerBackgroundLiftX = shift only the dark panel vs. the text (local pixels)")
            .AppendLine("// pointerTextBackgroundEnabled = panel behind the label for contrast")
            .AppendLine("// pointerTextBackgroundColor = hex (e.g. semi-transparent black #000000B0)")
            .AppendLine("// pointerTextBackgroundPadX / pointerTextBackgroundPadY = extra padding around text")
            .AppendLine("size=12")
            .AppendLine("color=#FFFFFFFF")
            .AppendLine("pointerTextEnabled=true")
            .AppendLine("pointerTextFontSize=22")
            .AppendLine("pointerTextColor=#FFFF88FF")
            .AppendLine("pointerLabelLiftX=0")
            .AppendLine("pointerLabelLiftY=48")
            .AppendLine("pointerBackgroundLiftX=0")
            .AppendLine("pointerBackgroundLiftY=0")
            .AppendLine("pointerTextBackgroundEnabled=true")
            .AppendLine("pointerTextBackgroundColor=#000000B0")
            .AppendLine("pointerTextBackgroundPadX=8")
            .AppendLine("pointerTextBackgroundPadY=4")
            .ToString();

    internal static void Initialize(MelonLogger.Instance log)
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                File.WriteAllText(ConfigPath, DefaultFileContents, Encoding.UTF8);
                log.Msg($"[DataCenterCrosshair] Created config file: {ConfigPath}");
            }
        }
        catch (System.Exception ex)
        {
            log.Warning($"[DataCenterCrosshair] Could not create config file: {ex.Message}");
        }

        ReloadFromDisk(force: true);
        log.Msg($"[DataCenterCrosshair] Edit crosshair here: {ConfigPath}");
    }

    /// <summary>Call each frame (or often); reloads when the file timestamp changes.</summary>
    internal static void TickReload()
    {
        ReloadFromDisk(force: false);
    }

    private static void ReloadFromDisk(bool force)
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return;

            long ticks = File.GetLastWriteTimeUtc(ConfigPath).Ticks;
            if (!force && ticks == _lastWriteTicks)
                return;

            _lastWriteTicks = ticks;
            Parse(File.ReadAllText(ConfigPath));
        }
        catch
        {
            // keep last good values
        }
    }

    private static void Parse(string text)
    {
        float size = _size;
        string hex = _colorHex;
        bool pointerEnabled = _pointerTextEnabled;
        float pointerFont = _pointerTextFontSize;
        string pointerHex = _pointerTextColorHex;
        float pointerOx = _pointerTextOffsetX;
        float pointerOy = _pointerTextOffsetY;
        bool pointerBg = _pointerTextBackgroundEnabled;
        string pointerBgHex = _pointerTextBackgroundColorHex;
        float pointerBgPadX = _pointerTextBackgroundPadX;
        float pointerBgPadY = _pointerTextBackgroundPadY;
        float pointerBgLiftX = _pointerBackgroundLiftX;
        float pointerBgLiftY = _pointerBackgroundLiftY;
        var any = false;

        var normalized = text.Replace("\r\n", "\n", System.StringComparison.Ordinal).Replace('\r', '\n');
        foreach (var segment in normalized.Split('\n'))
        {
            var line = segment.Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith("//", System.StringComparison.Ordinal))
                continue;

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = line.Substring(0, eq).Trim();
            var val = line.Substring(eq + 1).Trim();
            if (val.Length == 0)
                continue;

            if (key.Equals("size", System.StringComparison.OrdinalIgnoreCase)
                || key.Equals("crosshairsize", System.StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                {
                    size = s;
                    any = true;
                }
            }
            else if (key.Equals("color", System.StringComparison.OrdinalIgnoreCase)
                     || key.Equals("crosshaircolor", System.StringComparison.OrdinalIgnoreCase))
            {
                hex = NormalizeHex(val);
                any = true;
            }
            else if (key.Equals("pointerTextEnabled", System.StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseBool(val, out var b))
                {
                    pointerEnabled = b;
                    any = true;
                }
            }
            else if (key.Equals("pointerTextFontSize", System.StringComparison.OrdinalIgnoreCase)
                     || key.Equals("pointerTextSize", System.StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var fs))
                {
                    pointerFont = fs;
                    any = true;
                }
            }
            else if (key.Equals("pointerTextColor", System.StringComparison.OrdinalIgnoreCase))
            {
                pointerHex = NormalizeHex(val);
                any = true;
            }
            else if (key.Equals("pointerTextOffsetX", System.StringComparison.OrdinalIgnoreCase)
                     || key.Equals("pointerLabelLiftX", System.StringComparison.OrdinalIgnoreCase)
                     || key.Equals("labelLiftX", System.StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var ox))
                {
                    pointerOx = ox;
                    any = true;
                }
            }
            else if (key.Equals("pointerTextOffsetY", System.StringComparison.OrdinalIgnoreCase)
                     || key.Equals("pointerLabelLiftY", System.StringComparison.OrdinalIgnoreCase)
                     || key.Equals("labelLiftY", System.StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var oy))
                {
                    pointerOy = oy;
                    any = true;
                }
            }
            else if (key.Equals("pointerBackgroundLiftX", System.StringComparison.OrdinalIgnoreCase)
                     || key.Equals("backgroundLiftX", System.StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var lx))
                {
                    pointerBgLiftX = lx;
                    any = true;
                }
            }
            else if (key.Equals("pointerBackgroundLiftY", System.StringComparison.OrdinalIgnoreCase)
                     || key.Equals("backgroundLiftY", System.StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var ly))
                {
                    pointerBgLiftY = ly;
                    any = true;
                }
            }
            else if (key.Equals("pointerTextBackgroundEnabled", System.StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseBool(val, out var b))
                {
                    pointerBg = b;
                    any = true;
                }
            }
            else if (key.Equals("pointerTextBackgroundColor", System.StringComparison.OrdinalIgnoreCase))
            {
                pointerBgHex = NormalizeHex(val);
                any = true;
            }
            else if (key.Equals("pointerTextBackgroundPadX", System.StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
                {
                    pointerBgPadX = px;
                    any = true;
                }
            }
            else if (key.Equals("pointerTextBackgroundPadY", System.StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var py))
                {
                    pointerBgPadY = py;
                    any = true;
                }
            }
        }

        if (!any)
            return;

        _size = size;
        _colorHex = hex;
        _pointerTextEnabled = pointerEnabled;
        _pointerTextFontSize = pointerFont;
        _pointerTextColorHex = pointerHex;
        _pointerTextOffsetX = pointerOx;
        _pointerTextOffsetY = pointerOy;
        _pointerTextBackgroundEnabled = pointerBg;
        _pointerTextBackgroundColorHex = pointerBgHex;
        _pointerTextBackgroundPadX = pointerBgPadX;
        _pointerTextBackgroundPadY = pointerBgPadY;
        _pointerBackgroundLiftX = pointerBgLiftX;
        _pointerBackgroundLiftY = pointerBgLiftY;
    }

    private static bool TryParseBool(string val, out bool b)
    {
        if (val.Equals("1", System.StringComparison.Ordinal) || val.Equals("true", System.StringComparison.OrdinalIgnoreCase))
        {
            b = true;
            return true;
        }

        if (val.Equals("0", System.StringComparison.Ordinal) || val.Equals("false", System.StringComparison.OrdinalIgnoreCase))
        {
            b = false;
            return true;
        }

        b = false;
        return false;
    }

    /// <summary>Ensures leading # for ColorUtility; accepts RRGGBB or RRGGBBAA without #.</summary>
    private static string NormalizeHex(string val)
    {
        val = val.Trim();
        if (val.StartsWith("#", System.StringComparison.Ordinal))
            return val;
        if (val.Length is 6 or 8 && IsHexRun(val))
            return "#" + val;
        return val;
    }

    private static bool IsHexRun(string s)
    {
        foreach (var c in s)
        {
            if (char.IsDigit(c))
                continue;
            if (c is >= 'a' and <= 'f')
                continue;
            if (c is >= 'A' and <= 'F')
                continue;
            return false;
        }
        return true;
    }
}
