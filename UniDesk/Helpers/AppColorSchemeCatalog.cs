using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace UniDesk.Helpers;

public sealed class AppColorScheme
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required Color SwatchColor { get; init; }
    public required Color PrimaryBackground { get; init; }
    public required Color SecondaryBackground { get; init; }
    public required Color ModuleBackground { get; init; }
    public required Color Accent { get; init; }
    public required Color AccentSoft { get; init; }
    public required Color FocusRing { get; init; }
    public required Color Divider { get; init; }
}

public static class AppColorSchemeCatalog
{
    public const string DefaultSchemeId = "Taro";

    private static readonly AppColorScheme[] Schemes =
    [
        FromSwatch("Taro", "香芋紫", "#DCD0FF"),
        FromSwatch("KleinBlueLight", "克莱因蓝（淡化）", "#D1E8FF"),
        FromSwatch("DustyRose", "莫兰迪粉", "#D8A7B1", glassFactor: 0.48),
        FromSwatch("MatchaGreen", "抹茶绿", "#DBE9D7"),
        FromSwatch("HollyGreen", "冬青绿", "#D1DDC4"),
        Define(
            "LightGrey",
            "浅灰",
            "#94A3B8",
            "#E5475569",
            "#A5334154",
            "#66293241",
            "#CBD5E1",
            "#3DE2E8F0"),
        Define(
            "DarkGrey",
            "深灰",
            "#64748B",
            "#E52F3A47",
            "#A5243038",
            "#661C2833",
            "#94A3B8",
            "#3D64748B"),
        Define(
            "Black",
            "黑色",
            "#27272A",
            "#E5181818",
            "#A50F0F0F",
            "#66121212",
            "#A1A1AA",
            "#3D52525B")
    ];

    public static IReadOnlyList<AppColorScheme> All => Schemes;

    public static AppColorScheme Get(string? id)
    {
        return Schemes.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? Schemes.First(s => s.Id == DefaultSchemeId);
    }

    public static string NormalizeId(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return DefaultSchemeId;
        }

        return stored switch
        {
            "System" or "Light" => DefaultSchemeId,
            "Dark" => "KleinBlueLight",
            "SkyBlue" or "DeepBlue" or "IceBlue" => "KleinBlueLight",
            "Pink" or "Violet" or "SakuraPink" => "DustyRose",
            "PaleLilac" or "Periwinkle" => "Taro",
            "Mint" => "MatchaGreen",
            _ => Get(stored).Id
        };
    }

    public static void Apply(string? schemeId)
    {
        var scheme = Get(NormalizeId(schemeId));
        var dictionary = FindThemeDictionary();
        if (dictionary == null)
        {
            return;
        }

        // Calm Glass keeps surfaces and text neutral. A saved scheme supplies only
        // the accent and its derived interaction colors.
        SetColor(dictionary, "AccentColor", scheme.Accent);
        SetColor(dictionary, "AccentSoftColor", scheme.AccentSoft);
        SetColor(dictionary, "FocusRingColor", scheme.FocusRing);
    }

    private static AppColorScheme Define(
        string id,
        string displayName,
        string swatchHex,
        string primaryHex,
        string secondaryHex,
        string moduleHex,
        string accentHex,
        string dividerHex)
    {
        var accent = ColorFromHex(accentHex);
        return new()
        {
            Id = id,
            DisplayName = displayName,
            SwatchColor = ColorFromHex(swatchHex),
            PrimaryBackground = ColorFromHex(primaryHex),
            SecondaryBackground = ColorFromHex(secondaryHex),
            ModuleBackground = ColorFromHex(moduleHex),
            Accent = accent,
            AccentSoft = WithAlpha(accent, 0x3D),
            FocusRing = WithAlpha(accent, 0xCC),
            Divider = ColorFromHex(dividerHex)
        };
    }

    private static AppColorScheme FromSwatch(string id, string displayName, string swatchHex, double glassFactor = 0.44)
    {
        var swatch = ColorFromHex(swatchHex);
        var glass = BuildGlassPalette(swatch, glassFactor);
        var accent = glass.Accent;
        return new AppColorScheme
        {
            Id = id,
            DisplayName = displayName,
            SwatchColor = swatch,
            PrimaryBackground = glass.Primary,
            SecondaryBackground = glass.Secondary,
            ModuleBackground = glass.Module,
            Accent = accent,
            AccentSoft = WithAlpha(accent, 0x3D),
            FocusRing = WithAlpha(accent, 0xCC),
            Divider = glass.Divider
        };
    }

    private static (Color Primary, Color Secondary, Color Module, Color Accent, Color Divider) BuildGlassPalette(
        Color swatch,
        double glassFactor)
    {
        static byte Tone(byte channel, double factor) =>
            (byte)Math.Clamp(channel * factor, 28, 220);

        var primary = Color.FromArgb(
            0xE5,
            Tone(swatch.R, glassFactor),
            Tone(swatch.G, glassFactor),
            Tone(swatch.B, glassFactor));

        var secondary = Color.FromArgb(
            0xA5,
            Tone(swatch.R, glassFactor * 0.9),
            Tone(swatch.G, glassFactor * 0.9),
            Tone(swatch.B, glassFactor * 0.9));

        var module = Color.FromArgb(
            0x66,
            Tone(swatch.R, glassFactor * 0.78),
            Tone(swatch.G, glassFactor * 0.78),
            Tone(swatch.B, glassFactor * 0.78));

        var accent = Color.FromArgb(
            255,
            (byte)Math.Min(255, swatch.R + 18),
            (byte)Math.Min(255, swatch.G + 18),
            (byte)Math.Min(255, swatch.B + 18));

        var divider = Color.FromArgb(0x3D, accent.R, accent.G, accent.B);
        return (primary, secondary, module, accent, divider);
    }

    private static ResourceDictionary? FindThemeDictionary()
    {
        if (Application.Current == null)
        {
            return null;
        }

        return FindThemeDictionary(Application.Current.Resources)
               ?? Application.Current.Resources.MergedDictionaries
                   .Select(FindThemeDictionary)
                   .FirstOrDefault(d => d != null);
    }

    private static ResourceDictionary? FindThemeDictionary(ResourceDictionary dictionary)
    {
        if (dictionary.Contains("PrimaryBackgroundColor"))
        {
            return dictionary;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            var found = FindThemeDictionary(merged);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void SetColor(ResourceDictionary dictionary, string colorKey, Color color)
    {
        dictionary[colorKey] = color;

        var brushKey = colorKey.Replace("Color", "Brush", StringComparison.Ordinal);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        dictionary[brushKey] = brush;
    }

    private static Color ColorFromHex(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;

    private static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);
}
