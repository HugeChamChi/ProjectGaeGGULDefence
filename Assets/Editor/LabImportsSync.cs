using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 실험실 작업 폴더(outputs/workspaces/unity-ui)의 Assets/Imports를 루트 Develop 폴더에서 받아 맞춘다 (한 방향: 루트 → 실험실).
/// Rclone_Sync_System_Specs.md 규칙을 따른다 — 루트 Develop PC의 meta/GUID가 정답.
///   · 실험실에 없는 원본: 루트 .meta(새로 생기는 상위 폴더 .meta 포함)를 먼저 복사한 뒤 원본을 복사 (Unity가 새 GUID를 만들지 않게)
///   · 내용이 다른 원본: 루트 것으로 덮어쓰고, 이전 파일은 Temp/LabImportsSyncBackup/시각/ 에 남긴다
///   · 실험실에 이미 있는 .meta는 건드리지 않는다. 실험실에서 지우는 파일도 없다.
///   · 제외: Unity가 양쪽에서 따로 고치는 TMP 폰트 SDF(*SDF.asset), 세션 로그(production/)
/// 비교는 크기 → (크기가 같고 수정 시각이 다르면) MD5. 루트 프로젝트에서 실행하면 아무것도 하지 않는다.
/// </summary>
public static class LabImportsSync
{
    private const string RootPathPrefKey = "LabImportsSync_RootProjectPath";
    private const string BackupFolder = "Temp/LabImportsSyncBackup";
    private static readonly string[] ExcludedSuffixes = { "SDF.asset" };
    private static readonly string[] ExcludedSegments = { "/production/" };

    private sealed class Plan
    {
        public readonly List<string> Added = new List<string>();
        public readonly List<string> Changed = new List<string>();
        public readonly List<string> MetasToCopy = new List<string>();
        public readonly List<string> Excluded = new List<string>();
        public string RootImports;
        public string LabImports;
    }

    [MenuItem("Tools/USW/Lab/Imports - Preview Sync From Root")]
    public static void Preview()
    {
        var plan = BuildPlan();
        if (plan == null) return;
        Debug.Log(Describe(plan, "[LabImportsSync] 미리보기 (아무것도 바꾸지 않음)"));
    }

    [MenuItem("Tools/USW/Lab/Imports - Sync From Root")]
    public static void Sync()
    {
        var plan = BuildPlan();
        if (plan == null) return;
        if (plan.Added.Count == 0 && plan.Changed.Count == 0)
        {
            Debug.Log("[LabImportsSync] 이미 루트와 같다 — 받을 파일 없음");
            return;
        }

        string backupRoot = Path.Combine(ProjectRoot(), BackupFolder, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        try
        {
            // .meta 먼저 — Refresh 전에 원본과 짝을 맞춰 둔다
            foreach (var rel in plan.MetasToCopy) CopyFile(plan.RootImports, plan.LabImports, rel);
            foreach (var rel in plan.Added) CopyFile(plan.RootImports, plan.LabImports, rel);
            foreach (var rel in plan.Changed)
            {
                string labFile = Path.Combine(plan.LabImports, rel);
                string backup = Path.Combine(backupRoot, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(labFile, backup, true);
                CopyFile(plan.RootImports, plan.LabImports, rel);
            }
        }
        finally
        {
            AssetDatabase.Refresh();
        }
        Debug.Log(Describe(plan, $"[LabImportsSync] 받기 완료 (덮어쓴 파일 백업: {backupRoot})"));
    }

    private static Plan BuildPlan()
    {
        string labProject = ProjectRoot();
        string rootProject = FindRootProject(labProject);
        if (rootProject == null)
        {
            Debug.LogError("[LabImportsSync] 루트 Develop 프로젝트를 찾지 못했다. 실험실 작업 폴더에서만 쓴다 " +
                           $"(EditorPrefs '{RootPathPrefKey}'로 루트 경로를 지정할 수 있다)");
            return null;
        }

        var plan = new Plan
        {
            RootImports = Path.Combine(rootProject, "Assets", "Imports"),
            LabImports = Path.Combine(labProject, "Assets", "Imports"),
        };

        foreach (var src in Directory.EnumerateFiles(plan.RootImports, "*", SearchOption.AllDirectories))
        {
            string rel = src.Substring(plan.RootImports.Length + 1);
            if (rel.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            if (IsExcluded(rel)) { plan.Excluded.Add(rel); continue; }

            string dst = Path.Combine(plan.LabImports, rel);
            if (!File.Exists(dst))
            {
                plan.Added.Add(rel);
                AddFolderMetas(plan, rel);
                if (File.Exists(src + ".meta") && !File.Exists(dst + ".meta")) plan.MetasToCopy.Add(rel + ".meta");
                continue;
            }
            if (Differs(src, dst)) plan.Changed.Add(rel);
        }
        return plan;
    }

    // 실험실에 아직 없는 상위 폴더의 .meta도 루트 것을 먼저 놓는다 — 없으면 Unity가 폴더 GUID를 새로 만든다 (2026-10-12 첫 동기화에서 확인)
    private static void AddFolderMetas(Plan plan, string rel)
    {
        string dir = Path.GetDirectoryName(rel);
        while (!string.IsNullOrEmpty(dir))
        {
            string meta = dir + ".meta";
            if (!Directory.Exists(Path.Combine(plan.LabImports, dir))
                && File.Exists(Path.Combine(plan.RootImports, meta))
                && !File.Exists(Path.Combine(plan.LabImports, meta))
                && !plan.MetasToCopy.Contains(meta))
                plan.MetasToCopy.Add(meta);
            dir = Path.GetDirectoryName(dir);
        }
    }

    private static bool IsExcluded(string rel)
    {
        string unix = "/" + rel.Replace('\\', '/');
        foreach (var s in ExcludedSuffixes) if (unix.EndsWith(s, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var s in ExcludedSegments) if (unix.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static bool Differs(string a, string b)
    {
        var fa = new FileInfo(a);
        var fb = new FileInfo(b);
        if (fa.Length != fb.Length) return true;
        if (fa.LastWriteTimeUtc == fb.LastWriteTimeUtc) return false;
        return !SameHash(a, b);
    }

    private static bool SameHash(string a, string b)
    {
        using var md5 = MD5.Create();
        byte[] ha, hb;
        using (var sa = File.OpenRead(a)) ha = md5.ComputeHash(sa);
        using (var sb = File.OpenRead(b)) hb = md5.ComputeHash(sb);
        if (ha.Length != hb.Length) return false;
        for (int i = 0; i < ha.Length; i++) if (ha[i] != hb[i]) return false;
        return true;
    }

    private static void CopyFile(string fromRoot, string toRoot, string rel)
    {
        string src = Path.Combine(fromRoot, rel);
        string dst = Path.Combine(toRoot, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        File.Copy(src, dst, true);
        File.SetLastWriteTimeUtc(dst, File.GetLastWriteTimeUtc(src)); // 다음 비교를 빠르게
    }

    private static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    // 기본: 실험실 = <루트>/outputs/workspaces/<이름> → 세 단계 위가 루트. 루트에서 실행하면 null.
    private static string FindRootProject(string labProject)
    {
        string pref = EditorPrefs.GetString(RootPathPrefKey, "");
        string candidate = !string.IsNullOrEmpty(pref) ? pref : Path.GetFullPath(Path.Combine(labProject, "..", "..", ".."));
        if (string.Equals(Path.GetFullPath(candidate).TrimEnd('\\', '/'), labProject.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return null;
        return Directory.Exists(Path.Combine(candidate, "Assets", "Imports")) ? Path.GetFullPath(candidate) : null;
    }

    private static string Describe(Plan plan, string title)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{title}\n루트: {plan.RootImports}\n실험실: {plan.LabImports}");
        sb.AppendLine($"새 파일 {plan.Added.Count} (meta 함께 {plan.MetasToCopy.Count}) · 바뀐 파일 {plan.Changed.Count} · 제외 {plan.Excluded.Count}");
        foreach (var r in plan.Added) sb.AppendLine("  + " + r);
        foreach (var r in plan.Changed) sb.AppendLine("  * " + r);
        foreach (var r in plan.Excluded) sb.AppendLine("  (제외) " + r);
        return sb.ToString();
    }
}
