using System;
using UnityEngine;

/// <summary>Build-time imported account growth trial values. Independent of combat EXP.</summary>
[CreateAssetMenu(menuName = "Game/Account Progression Config")]
public sealed class AccountProgressionConfig : ScriptableObject
{
    /// <summary>Requirement at index level-1; the final level has no next requirement (zero).</summary>
    public int[] RequiredExperience;
    /// <summary>Initial account level authored in the workbook.</summary>
    public int InitialLevel;
    /// <summary>Initial account EXP authored in the workbook.</summary>
    public int InitialExperience;
    /// <summary>Trial terminal policies shared by production battle modes.</summary>
    public bool GrantOnWin;
    /// <summary>Trial defeat policy.</summary>
    public bool GrantOnLose;
    /// <summary>EXP for one boss at 100% progress on victory.</summary>
    public int WinBossExperience;
    /// <summary>EXP for one boss at 100% progress on defeat.</summary>
    public int LoseBossExperience;
    /// <summary>Imported source fingerprint for reproducibility.</summary>
    public string SourceSha256;
    /// <summary>Maximum authored account level.</summary>
    public int MaxLevel => RequiredExperience?.Length ?? 0;

    /// <summary>Rejects malformed data rather than inventing fallback requirements.</summary>
    public void Validate()
    {
        if (MaxLevel < 2 || InitialLevel < 1 || InitialLevel > MaxLevel || InitialExperience < 0
            || WinBossExperience < 0 || LoseBossExperience < 0)
            throw new InvalidOperationException("Invalid account progression configuration.");
        long total = 0;
        for (int i = 0; i < MaxLevel - 1; i++)
        {
            if (RequiredExperience[i] <= 0) throw new InvalidOperationException($"Account level {i + 1}: requirement must be positive.");
            total += RequiredExperience[i];
        }
        if (total > int.MaxValue || RequiredExperience[MaxLevel - 1] != 0
            || (InitialLevel == MaxLevel ? InitialExperience != 0 : InitialExperience >= RequiredExperience[InitialLevel - 1]))
            throw new InvalidOperationException("Invalid account curve total, cap or initial EXP.");
    }

    /// <summary>Computes carry-over/multiple level-ups, returning only EXP retained before the cap.</summary>
    public int Apply(int level, int experience, int grant, out int nextLevel, out int nextExperience)
    {
        Validate();
        if (level < 1 || level > MaxLevel || experience < 0 || grant < 0)
            throw new InvalidOperationException("Invalid saved account progression.");
        nextLevel = level;
        long remaining = (long)experience + grant;
        long consumed = 0;
        while (nextLevel < MaxLevel && remaining >= RequiredExperience[nextLevel - 1])
        {
            int cost = RequiredExperience[nextLevel - 1];
            remaining -= cost; consumed += cost; nextLevel++;
        }
        nextExperience = nextLevel == MaxLevel ? 0 : checked((int)remaining);
        return checked((int)Math.Max(0, Math.Min(grant, consumed + nextExperience - experience)));
    }
}
