using UnityEngine;

public static class ColorHex
{
    public static Color Hex(string hex)
    {
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (ColorUtility.TryParseHtmlString(hex, out var c))
            return c;
        return Color.white;
    }
}