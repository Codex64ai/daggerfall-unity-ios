// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/IniParser/IniUtils.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Globalization;

public struct DoubleString
{
    public string Item1;
    public string Item2;
}

public static class IniUtils
{
    public static string IniComment(string comment)
    {
        return "; " + comment;
    }

    public static string IniSection(string sectionName)
    {
        return "[" + sectionName + "]";
    }

    public static string IniProperty(string property, string value)
    {
        return property + "=" + value;
    }

    public static DoubleString UnpackIniProperty(string packed)
    {
        var parts = packed.Split('=');
        return new DoubleString()
        {
            Item1 = parts[0].Trim(),
            Item2 = parts[1].Trim()
        };
    }

    public static string SerializeVector2(Vector2 val)
    {
        return val.x.ToString(CultureInfo.InvariantCulture) + "," + val.y.ToString(CultureInfo.InvariantCulture);
    }

    public static Vector2 DeserializeVector2(string serialized)
    {
        var parts = serialized.Split(',');
        var x = float.Parse(parts[0], CultureInfo.InvariantCulture);
        var y = float.Parse(parts[1], CultureInfo.InvariantCulture);

        return new Vector2(x, y);
    }
}
