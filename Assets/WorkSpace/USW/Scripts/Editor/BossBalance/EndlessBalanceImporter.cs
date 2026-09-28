using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>무한/패널티 엑셀 입력을 초안 또는 검증된 설정 SO 묶음으로 반영한다.</summary>
public static class EndlessBalanceImporter
{
    /// <summary>제작 설정 SO를 저장하는 기본 폴더.</summary>
    public const string DefaultAssetRoot = "Assets/WorkSpace/USW/Data/Endless";

    /// <summary>한 번의 검증 결과. Apply는 변경 여부를 다시 검사한다.</summary>
    public sealed class Plan
    {
        /// <summary>검증한 XLSX의 절대 경로.</summary>
        public string WorkbookPath;
        /// <summary>검증/적용 대상 SO 폴더.</summary>
        public string AssetRoot;
        /// <summary>입력과 대상 상태의 검증 해시.</summary>
        public string Fingerprint;
        /// <summary>필드별 이전 값과 새 값 및 활성화 제한.</summary>
        public string Summary;
        /// <summary>생성 또는 수정할 SO 개수.</summary>
        public int Changes;
    }

    private sealed class Item
    {
        internal string Path;
        internal ScriptableObject Desired;
        internal ScriptableObject Existing;
        internal string Before;
        internal bool Changed;
        internal string Difference;
    }

    private sealed class Graph : IDisposable
    {
        internal readonly List<Item> Items = new();
        internal readonly List<string> Notes = new();
        internal string Fingerprint;
        internal string Root;
        public void Dispose() { foreach (var item in Items) UnityEngine.Object.DestroyImmediate(item.Desired); }
    }

    /// <summary>전 입력과 키 연결을 검증하고 변경 목록을 만든다. 초안은 활성화하지 않는다.</summary>
    public static Plan Prepare(string workbookPath, BossBalanceRegistry registry, string assetRoot = DefaultAssetRoot)
    {
        using var graph = Build(workbookPath, registry, assetRoot);
        var changed = graph.Items.Where(x => x.Changed).ToArray();
        return new Plan
        {
            WorkbookPath = Path.GetFullPath(workbookPath), AssetRoot = graph.Root, Fingerprint = graph.Fingerprint, Changes = changed.Length,
            Summary = $"무한 설정 {graph.Items.Count}개 검증 / 변경 {changed.Length}개\n" +
                string.Join("\n", changed.Select(x => (x.Existing == null ? "생성: " : "갱신: ") + x.Path + "\n" + x.Difference)) +
                "\n\n" + string.Join("\n", graph.Notes)
        };
    }

    /// <summary>검증 시점과 동일한 설정 묶음만 적용한다. 실패 시 이전 SO와 참조를 복원한다.</summary>
    public static string Apply(Plan preview, BossBalanceRegistry registry)
    {
        if (preview == null) throw new ArgumentNullException(nameof(preview));
        using var graph = Build(preview.WorkbookPath, registry, preview.AssetRoot);
        if (graph.Fingerprint != preview.Fingerprint) throw new InvalidOperationException("검증 이후 엑셀/설정/등록표가 변경되었습니다. 다시 검증하세요.");
        var changed = graph.Items.Where(x => x.Changed).ToArray();
        if (changed.Length == 0) return "변경 없음";
        string backup = "outputs/endless-import/backups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        Directory.CreateDirectory(backup);
        foreach (var item in changed.Where(x => x.Existing != null))
        {
            string saved = backup + "/" + item.Path;
            Directory.CreateDirectory(Path.GetDirectoryName(saved));
            File.Copy(item.Path, saved);
        }
        File.WriteAllText(backup + "/changes.txt", preview.Summary, Encoding.UTF8);
        var created = new List<string>();
        try
        {
            foreach (var item in changed.Where(x => x.Existing == null))
            {
                EnsureFolder(Path.GetDirectoryName(item.Path).Replace('\\', '/'));
                var target = ScriptableObject.CreateInstance(item.Desired.GetType());
                AssetDatabase.CreateAsset(target, item.Path);
                item.Existing = target;
                created.Add(item.Path);
            }
            var actual = graph.Items.ToDictionary(x => (UnityEngine.Object)x.Desired, x => (UnityEngine.Object)x.Existing);
            foreach (var item in changed)
            {
                EditorUtility.CopySerialized(item.Desired, item.Existing);
                var serialized = new SerializedObject(item.Existing);
                var iterator = serialized.GetIterator();
                while (iterator.Next(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue != null && actual.TryGetValue(iterator.objectReferenceValue, out var replacement))
                        iterator.objectReferenceValue = replacement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item.Existing);
            }
            foreach (var item in changed) AssetDatabase.SaveAssetIfDirty(item.Existing);
        }
        catch
        {
            foreach (var item in changed.Where(x => !created.Contains(x.Path)))
            {
                EditorJsonUtility.FromJsonOverwrite(item.Before, item.Existing);
                File.Copy(backup + "/" + item.Path, item.Path, true);
                AssetDatabase.ImportAsset(item.Path, ImportAssetOptions.ForceUpdate);
            }
            foreach (string path in created) AssetDatabase.DeleteAsset(path);
            throw;
        }
        return backup;
    }

    private static Graph Build(string path, BossBalanceRegistry registry, string assetRoot)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 Import하세요.");
        if (registry == null) throw new InvalidOperationException("보스 키 등록표가 없습니다.");
        if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new FormatException(".xlsx 파일을 선택하세요.");
        byte[] bytes = File.ReadAllBytes(path);
        string root = Path.GetFullPath(assetRoot).Replace('\\', '/');
        string assets = Path.GetFullPath("Assets").Replace('\\', '/') + "/";
        if (!root.StartsWith(assets, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("설정 폴더는 Assets 하위여야 합니다.");
        var graph = new Graph { Root = "Assets/" + root.Substring(assets.Length).TrimEnd('/') };
        try
        {
            var bosses = new Dictionary<string, BossData>(StringComparer.Ordinal);
            foreach (var b in registry.Bosses)
            {
                if (b == null || !EndlessConfigurationValidator.IsValidKey(b.Key) || b.Data == null || bosses.ContainsKey(b.Key)) throw new FormatException("보스 등록표 연결 누락/중복");
                bosses.Add(b.Key, b.Data);
            }
            var rules = Rows(bytes, "Stage_무한", "규칙 키", "값").ToDictionary(x => Key(x, "규칙 키"), x => x.Get("값"), StringComparer.Ordinal);
            int Rule(string key) => PositiveInteger(rules.TryGetValue(key, out string value) ? value : "", "Stage_무한 " + key);
            // Workbooks predating v2 keep their fixed-cycle behavior.
            var selection = EndlessBossSelection.FixedCycle;
            if (rules.TryGetValue("BossSelection", out string selectionText) &&
                (!Enum.TryParse(selectionText, false, out selection) || !Enum.IsDefined(typeof(EndlessBossSelection), selection)))
                throw new FormatException("Stage_무한 BossSelection: FixedCycle 또는 ShuffleAfterOpening을 입력하세요.");
            var bossPools = new Dictionary<string, EndlessBossPoolData>(StringComparer.Ordinal);
            if (rules.ContainsKey("BossSelection"))
            {
                foreach (var group in Rows(bytes, "Stage_보스풀", "풀 키", "보스 분류", "보스 키", "추첨 사용").GroupBy(x => Key(x, "풀 키")))
                {
                    var data = Add<EndlessBossPoolData>(graph, "BossPools", group.Key);
                    var rows = group.ToArray();
                    int role = BossRole(rows[0], "보스 분류", false);
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    var definitions = new HashSet<BossData>();
                    var entries = new List<(string Key, BossData Boss, bool Enabled)>();
                    foreach (var row in rows)
                    {
                        if (BossRole(row, "보스 분류", false) != role) throw row.Error("보스 분류", "같은 풀은 한 분류만 사용합니다.");
                        bool enabled = Yes(row, "추첨 사용");
                        string bossKey = row.Get("보스 키");
                        if (string.IsNullOrEmpty(bossKey) && !enabled) continue; // Empty draft pool declaration.
                        if (!EndlessConfigurationValidator.IsValidKey(bossKey) || !bosses.TryGetValue(bossKey, out var boss))
                            throw row.Error("보스 키", "빈 값 또는 미등록 보스");
                        if (!keys.Add(bossKey) || !definitions.Add(boss)) throw row.Error("보스 키", "같은 풀의 보스 정의 중복");
                        if (enabled && boss.Prefab == null) throw row.Error("보스 키", "사용 후보의 프리팹 누락");
                        entries.Add((bossKey, boss, enabled));
                    }
                    Edit(data, s =>
                    {
                        Set(s, "_key", group.Key); Set(s, "_role", role);
                        var list = s.FindProperty("_entries"); list.arraySize = entries.Count;
                        for (int i = 0; i < entries.Count; i++)
                        {
                            var entry = list.GetArrayElementAtIndex(i);
                            entry.FindPropertyRelative("_bossKey").stringValue = entries[i].Key;
                            entry.FindPropertyRelative("_boss").objectReferenceValue = entries[i].Boss;
                            entry.FindPropertyRelative("_enabled").boolValue = entries[i].Enabled;
                        }
                    });
                    bossPools.Add(group.Key, data);
                }
            }
            var penalties = new Dictionary<string, RunPenaltyData>(StringComparer.Ordinal);
            foreach (var row in Rows(bytes, "Stage_패널티", "패널티 키", "이름", "증감값", "단위", "상태", "효과 대상"))
            {
                string key = Key(row, "패널티 키");
                if (penalties.ContainsKey(key)) throw row.Error("패널티 키", "중복 키");
                var data = Add<RunPenaltyData>(graph, "Penalties", key);
                string state = row.Get("상태");
                if (state != "사용" && state != "수치 미정") throw row.Error("상태", "사용 또는 수치 미정으로 입력하세요.");
                bool configured = state == "사용";
                string unitText = row.Get("단위");
                RunPenaltyUnit unit = unitText == "%" ? RunPenaltyUnit.Percent : unitText == "고정" ? RunPenaltyUnit.Flat : unitText == "" ? RunPenaltyUnit.Unspecified : throw row.Error("단위", "%, 고정 또는 미정 공란만 지원합니다.");
                if (!Enum.TryParse(row.Get("효과 대상"), false, out RunPenaltyTarget target) || !Enum.IsDefined(typeof(RunPenaltyTarget), target) || target == RunPenaltyTarget.Unspecified) throw row.Error("효과 대상", "잘못된 대상");
                double delta = Number(row, "증감값", !configured);
                if (configured && unit == RunPenaltyUnit.Unspecified) throw row.Error("단위", "사용 항목의 단위가 없습니다.");
                string displayName = row.Get("이름");
                if (string.IsNullOrWhiteSpace(displayName)) throw row.Error("이름", "이름이 필요합니다.");
                Edit(data, s => { Set(s, "_key", key); Set(s, "_displayName", displayName); Set(s, "_target", (int)target); Set(s, "_unit", (int)unit); Set(s, "_delta", delta); Set(s, "_isConfigured", configured); });
                if (configured && !EndlessConfigurationValidator.IsSupportedPenalty(data)) throw row.Error("증감값", "현재 런타임은 유닛 공격력/공격 빈도의 -100 초과 0 이하 %만 지원합니다.");
                penalties.Add(key, data);
            }
            var pools = new Dictionary<string, RunPenaltyPoolData>(StringComparer.Ordinal);
            foreach (var group in Rows(bytes, "Stage_패널티풀", "풀 키", "패널티 키", "추첨 사용", "가중치", "가중치 확정").GroupBy(x => Key(x, "풀 키")))
            {
                var data = Add<RunPenaltyPoolData>(graph, "Pools", group.Key);
                var entries = group.ToArray();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                Edit(data, s =>
                {
                    Set(s, "_key", group.Key);
                    var list = s.FindProperty("_entries");list.arraySize = entries.Length;
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var row = entries[i];string key = Key(row, "패널티 키");
                        if (!seen.Add(key) || !penalties.TryGetValue(key, out var penalty)) throw row.Error("패널티 키", "중복/미등록 키");
                        bool enabled = Yes(row, "추첨 사용");
                        double weight = Number(row, "가중치", !enabled);
                        if (weight < 0 || (enabled && weight <= 0)) throw row.Error("가중치", "사용 후보는 양수 가중치가 필요합니다.");
                        if (enabled && !EndlessConfigurationValidator.IsSupportedPenalty(penalty)) throw row.Error("추첨 사용", "미정/미지원 효과는 활성화할 수 없습니다.");
                        var entry = list.GetArrayElementAtIndex(i);
                        entry.FindPropertyRelative("_penalty").objectReferenceValue = penalty;
                        entry.FindPropertyRelative("_enabled").boolValue = enabled;
                        entry.FindPropertyRelative("_weight").doubleValue = weight;
                        entry.FindPropertyRelative("_weightConfirmed").boolValue = Yes(row, "가중치 확정");
                    }
                });
                pools.Add(group.Key, data);
            }
            var slots = Rows(bytes, "Stage_보스순환", "주기 순서", "보스 난도", "보스 키", "난이도 키").ToArray();
            var modes = new HashSet<string>(StringComparer.Ordinal);
            var modeHeaders = new List<string> { "난이도 키", "활성", "성장 확정", "첫 성장 라운드", "성장 간격", "HP 증가(%)", "방어 증가", "EXP 증가(%)", "패널티 풀" };
            if (rules.ContainsKey("BossSelection")) modeHeaders.AddRange(new[] { "보통 보스 풀", "어려운 보스 풀" });
            foreach (var row in Rows(bytes, "Stage_난이도", modeHeaders.ToArray()))
            {
                string key = Key(row, "난이도 키");
                if (!modes.Add(key)) throw row.Error("난이도 키", "중복 키");
                var data = Add<EndlessModeData>(graph, "Modes", key);
                if (!pools.TryGetValue(Key(row, "패널티 풀"), out var pool)) throw row.Error("패널티 풀", "미등록 풀");
                EndlessBossPoolData BossPool(string field, EndlessBossRole role)
                {
                    string poolKey = row.Get(field);
                    if (!rules.ContainsKey("BossSelection") || string.IsNullOrEmpty(poolKey)) return null;
                    if (!bossPools.TryGetValue(poolKey, out var value) || value.Role != role) throw row.Error(field, "미등록 풀 또는 분류 불일치");
                    return value;
                }
                var breatherPool = BossPool("보통 보스 풀", EndlessBossRole.Breather);
                var hardPool = BossPool("어려운 보스 풀", EndlessBossRole.Hard);
                var matching = slots.Where(x => x.Get("난이도 키") == key || x.Get("난이도 키") == "ALL").ToArray();
                var resolved = matching.GroupBy(x => PositiveInteger(x.Get("주기 순서"), "Stage_보스순환 주기 순서")).Select(g =>
                {
                    var own = g.Where(x => x.Get("난이도 키") == key).ToArray();
                    var common = g.Where(x => x.Get("난이도 키") == "ALL").ToArray();
                    if (own.Length > 1 || common.Length > 1) throw new FormatException("Stage_보스순환: 같은 난이도/위치 중복");
                    return own.Length == 1 ? own[0] : common[0];
                }).OrderBy(x => PositiveInteger(x.Get("주기 순서"), "주기 순서")).ToArray();
                if (resolved.Length != Rule("CycleLength") || resolved.Any(x => PositiveInteger(x.Get("주기 순서"), "주기 순서") > Rule("CycleLength")))
                    throw row.Error("난이도 키", "주기 길이의 모든 위치를 중복 없이 채워야 합니다.");
                bool configured = Yes(row, "성장 확정");
                Edit(data, s =>
                {
                    Set(s, "_key", key);Set(s, "_enabled", Yes(row, "활성"));
                    Set(s, "_cycleLength", Rule("CycleLength"));Set(s, "_penaltyInterval", Rule("PenaltyInterval"));
                    Set(s, "_penaltyFirstApplyRound", Rule("PenaltyStart"));Set(s, "_penaltyDrawCount", Rule("PenaltyCount"));
                    Set(s, "_penaltyPool", pool);
                    Set(s, "_bossSelection", (int)selection);
                    Set(s, "_breatherBossPool", breatherPool); Set(s, "_hardBossPool", hardPool);
                    var list = s.FindProperty("_slots");list.arraySize = resolved.Length;
                    for (int i = 0; i < resolved.Length; i++)
                    {
                        var slot = resolved[i];string bossKey = slot.Get("보스 키");
                        BossData boss = null;
                        if (!string.IsNullOrEmpty(bossKey) && (!EndlessConfigurationValidator.IsValidKey(bossKey) || !bosses.TryGetValue(bossKey, out boss))) throw slot.Error("보스 키", "미등록 키");
                        int roleValue = BossRole(slot, "보스 난도", true);
                        var entry = list.GetArrayElementAtIndex(i);
                        entry.FindPropertyRelative("_position").intValue = PositiveInteger(slot.Get("주기 순서"), "주기 순서");
                        entry.FindPropertyRelative("_role").intValue = roleValue;
                        entry.FindPropertyRelative("_bossKey").stringValue = bossKey;
                        entry.FindPropertyRelative("_boss").objectReferenceValue = boss;
                    }
                    Set(s, "_growth._isConfigured", configured);
                    Set(s, "_growth._firstGrowthRound", OptionalPositiveInteger(row, "첫 성장 라운드", configured));
                    Set(s, "_growth._intervalRounds", OptionalPositiveInteger(row, "성장 간격", configured));
                    foreach (var pair in new[] { ("_hpPercentPerStep", "HP 증가(%)"), ("_defenseAddedPerStep", "방어 증가"), ("_expPercentPerStep", "EXP 증가(%)") })
                    {
                        double value = Number(row, pair.Item2, !configured);
                        if (value < 0) throw row.Error(pair.Item2, "0 이상이어야 합니다.");
                        Set(s, "_growth." + pair.Item1, value);
                    }
                });
                bool valid = EndlessConfigurationValidator.TryValidateForRun(data, out string error);
                if (data.Enabled && !valid) throw row.Error("활성", error);
                graph.Notes.Add(data.Enabled ? key + ": 활성 설정 검증 완료" : key + ": 비활성 초안\n" + error);
            }
            if (penalties.Count == 0 || pools.Count == 0 || modes.Count == 0) throw new FormatException("효과/풀/난이도 입력이 비어 있습니다.");
            foreach (var slot in slots) if (slot.Get("난이도 키") != "ALL" && !modes.Contains(slot.Get("난이도 키"))) throw slot.Error("난이도 키", "미등록 난이도");
            foreach (var rule in new[] { ("PenaltySelection", "무작위"), ("PenaltyStack", "항목별 곱누적"), ("PenaltyLifetime", "현재 런") })
                if (!rules.TryGetValue(rule.Item1, out string value) || value != rule.Item2) throw new FormatException("Stage_무한: 지원되지 않은 규칙 " + rule.Item1);
            if (Rule("PenaltyCount") != 1) throw new FormatException("회당 1개 패널티 추첨만 지원합니다.");
            var fingerprint = new StringBuilder(Hash(bytes)).Append(EditorJsonUtility.ToJson(registry));
            var temporaryPaths = graph.Items.ToDictionary(x => (UnityEngine.Object)x.Desired, x => x.Path);
            foreach (var item in graph.Items)
            {
                item.Before = item.Existing == null ? "" : EditorJsonUtility.ToJson(item.Existing);
                if (item.Existing != null && EditorUtility.IsDirty(item.Existing)) throw new InvalidOperationException(item.Path + ": 미저장 변경을 먼저 저장/취소하세요.");
                var before = Snapshot(item.Existing, temporaryPaths);
                var after = Snapshot(item.Desired, temporaryPaths);
                var differences = before.Keys.Union(after.Keys).Where(k => !before.TryGetValue(k, out var b) || !after.TryGetValue(k, out var a) || b != a).ToArray();
                item.Changed = item.Existing == null || differences.Length > 0;
                item.Difference = string.Join("\n", differences.Select(k => "  " + k + ": " + (before.TryGetValue(k, out var b) ? b : "(없음)") + " → " + (after.TryGetValue(k, out var a) ? a : "(삭제)")));
                fingerprint.Append(item.Path).Append(item.Before);
                foreach (var field in after) fingerprint.Append(field.Key).Append('=').Append(field.Value).Append('\n');
            }
            graph.Fingerprint = Hash(Encoding.UTF8.GetBytes(fingerprint.ToString()));
            return graph;
        }
        catch { graph.Dispose(); throw; }
    }

    private static T Add<T>(Graph graph, string folder, string key) where T : ScriptableObject
    {
        // Resolve existing keys from assets, so asset renames do not create duplicates.
        T found = null;
        if (AssetDatabase.IsValidFolder(graph.Root))
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { graph.Root }))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (new SerializedObject(candidate).FindProperty("_key").stringValue != key) continue;
                if (found != null) throw new FormatException($"{typeof(T).Name}: SO의 키 중복 {key}");
                found = candidate;
            }
        string path = found != null ? AssetDatabase.GetAssetPath(found) : graph.Root + "/" + folder + "/" + key + ".asset";
        if (found == null && File.Exists(path)) throw new FormatException(path + ": 다른 키/타입의 에셋과 경로가 겹칩니다.");
        var desired = ScriptableObject.CreateInstance<T>();
        desired.name = found != null ? found.name : key;
        graph.Items.Add(new Item { Path = path, Existing = found, Desired = desired });
        return desired;
    }

    private static IEnumerable<BalanceXlsxTable.Row> Rows(byte[] bytes, string sheet, params string[] headers) => BalanceXlsxTable.Read(bytes, sheet, headers).Rows;
    // EditorJsonUtility serializes temporary SO references as instanceID:0. Compare actual
    // SerializedProperty references instead, mapping both sides to their intended asset path.
    private static SortedDictionary<string, string> Snapshot(UnityEngine.Object value, Dictionary<UnityEngine.Object, string> paths)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (value == null) return result;
        var iterator = new SerializedObject(value).GetIterator();
        bool enterChildren = true;
        while (iterator.Next(enterChildren))
        {
            // Traverse containers only; strings otherwise expand into redundant character-array diffs.
            enterChildren = iterator.propertyType == SerializedPropertyType.Generic;
            if (iterator.propertyPath.StartsWith("m_", StringComparison.Ordinal)) continue;
            string text;
            switch (iterator.propertyType)
            {
                case SerializedPropertyType.Integer: case SerializedPropertyType.ArraySize: case SerializedPropertyType.Enum:
                    text = iterator.longValue.ToString(CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Float: text = iterator.doubleValue.ToString("R", CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Boolean: text = iterator.boolValue ? "true" : "false"; break;
                case SerializedPropertyType.String: text = iterator.stringValue; break;
                case SerializedPropertyType.ObjectReference:
                    var reference = iterator.objectReferenceValue;
                    text = reference == null ? "(null)" : paths.TryGetValue(reference, out string mapped) ? mapped : AssetDatabase.GetAssetPath(reference); break;
                case SerializedPropertyType.Generic: continue;
                default: throw new InvalidOperationException("지원되지 않은 계약 필드: " + iterator.propertyPath);
            }
            result.Add(iterator.propertyPath, text);
        }
        return result;
    }
    private static string Key(BalanceXlsxTable.Row row, string field)
    {
        string key = row.Get(field);
        if (!EndlessConfigurationValidator.IsValidKey(key)) throw row.Error(field, "빈 값 또는 잘못된 키");
        return key;
    }
    private static int BossRole(BalanceXlsxTable.Row row, string field, bool allowUnspecified)
    {
        string role = row.Get(field);
        return role == "미정" && allowUnspecified ? 0 : role == "쉬어감" || role == "보통" ? 1 : role == "어려움" ? 2 :
            throw row.Error(field, allowUnspecified ? "미정/보통(쉬어감)/어려움 중 입력하세요." : "보통(쉬어감)/어려움 중 입력하세요.");
    }
    private static bool Yes(BalanceXlsxTable.Row row, string field) => row.Get(field) == "예" ? true : row.Get(field) == "아니오" ? false : throw row.Error(field, "예/아니오로 입력하세요.");
    private static double Number(BalanceXlsxTable.Row row, string field, bool optional)
    {
        string input = row.Get(field);
        if (optional && string.IsNullOrEmpty(input)) return 0d;
        if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value)) throw row.Error(field, "유한한 숫자가 필요합니다.");
        return value;
    }
    private static int PositiveInteger(string input, string source)
    {
        if (!decimal.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value) || value != decimal.Truncate(value) || value < 1 || value > int.MaxValue) throw new FormatException(source + ": 양의 정수가 필요합니다.");
        return (int)value;
    }
    private static int OptionalPositiveInteger(BalanceXlsxTable.Row row, string field, bool required) => !required && string.IsNullOrEmpty(row.Get(field)) ? 0 : PositiveInteger(row.Get(field), field);
    private static void Edit(ScriptableObject target, Action<SerializedObject> action)
    { var serialized = new SerializedObject(target);action(serialized);serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Set(SerializedObject serialized, string path, object value)
    {
        var property = serialized.FindProperty(path) ?? throw new InvalidOperationException("계약 필드 없음: " + path);
        if (value is string text) property.stringValue = text;
        else if (value is bool boolean) property.boolValue = boolean;
        else if (value is int integer) property.intValue = integer;
        else if (value is double number) property.doubleValue = number;
        else property.objectReferenceValue = (UnityEngine.Object)value;
    }
    private static string Hash(byte[] bytes) { using var sha = SHA256.Create();return BitConverter.ToString(sha.ComputeHash(bytes)); }
    private static void EnsureFolder(string path)
    { if (!AssetDatabase.IsValidFolder(path)) { string parent = Path.GetDirectoryName(path).Replace('\\', '/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); } }
}
