using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// PlayerPrefs string entries with a signature, for recovery journals that are replayed to the server.
/// An entry whose signature is missing or does not match is deleted and reported as absent, so text
/// written or edited by hand never reaches the server. The key must contain the owning account.
/// This only deters editing the saved file; memory or binary tampering needs the server-side
/// functions planned in docs/technical/server-reward-functions.md (TODO G16). Remove with G16 step 6.
/// </summary>
public static class SignedPrefs
{
    private const string Secret = "GGD.SignedPrefs.Journal.4b91d2c7";
    private const string SignatureSuffix = ".sig";

    /// <summary>True only when the entry exists and its signature matches. A failed entry is deleted.</summary>
    public static bool HasKey(string key)
    {
        if (!PlayerPrefs.HasKey(key)) return false;
        if (PlayerPrefs.GetString(key + SignatureSuffix, "") == Sign(key, PlayerPrefs.GetString(key))) return true;
        DeleteKey(key);
        PlayerPrefs.Save();
        Debug.LogWarning("[SignedPrefs] Discarded unverified entry: " + key);
        return false;
    }

    /// <summary>Verified value, or the default when the entry is absent or failed verification.</summary>
    public static string GetString(string key, string defaultValue = "") =>
        HasKey(key) ? PlayerPrefs.GetString(key) : defaultValue;

    /// <summary>Stores the value with its signature. The caller decides when to call PlayerPrefs.Save.</summary>
    public static void SetString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.SetString(key + SignatureSuffix, Sign(key, value));
    }

    /// <summary>Removes the value and its signature.</summary>
    public static void DeleteKey(string key)
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.DeleteKey(key + SignatureSuffix);
    }

    private static string Sign(string key, string value)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(key + "|" + value));
        return BitConverter.ToString(hash).Replace("-", "");
    }
}
