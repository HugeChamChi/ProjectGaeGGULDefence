using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 용어 사전 SO를 만들고 기본 4개 키워드(사용자 확정 2026-10-10)를 채운다.
/// 이미 있는 Id는 건드리지 않는다 — 만든 뒤에는 에셋이 문구의 원본이다.
/// 정의 출처: 사용자 전달 기획(족장 스킬·자폭 드론·해킹스택·시간조작), 시간 소비 규칙은 design/gdd/overdrive-deck.md §2 [확정].
/// </summary>
public static class TermGlossarySetup
{
    public const string AssetPath = "Assets/WorkSpace/USW/Data/UI/TermGlossary.asset";
    private const string DroneData = "Assets/WorkSpace/USW/Data/DroneUnits/";
    private const string ClipRoot = "Assets/WorkSpace/USW/Video/TermClips/";

    [MenuItem("Tools/USW/UI/Create Term Glossary")]
    public static void CreateOrFill()
    {
        var glossary = AssetDatabase.LoadAssetAtPath<TermGlossary>(AssetPath);
        if (glossary == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            glossary = ScriptableObject.CreateInstance<TermGlossary>();
            AssetDatabase.CreateAsset(glossary, AssetPath);
        }

        int added = 0;
        foreach (var entry in Defaults())
            if (glossary.AddIfMissing(entry)) added++;
        EditorUtility.SetDirty(glossary);
        AssetDatabase.SaveAssets();
        Debug.Log($"[TermGlossary] {AssetPath}: 추가 {added}개, 전체 {glossary.Entries.Count}개");
        Selection.activeObject = glossary;
    }

    private static TermEntry[] Defaults() => new[]
    {
        new TermEntry
        {
            Id = "chief_skill", DisplayName = "액티브 스킬", Aliases = new[] { "액티브 스킬", "족장 스킬", "알팡 액티브" },
            Kind = "족장", KindColor = new Color(0.62f, 0.38f, 0.95f),
            Body = "왼쪽 아래 족장 버튼을 눌러 직접 쓰는 기술입니다. 지금 족장 알팡은 드론을 모아 보스에게 일제 사격합니다. " +
                   "쿨타임이 다 차야 쓸 수 있고, 모일 드론이 없으면 발동하지 않습니다. 악어 보스가 지진을 예고하는 동안 쓰면 그 예고를 끊습니다. " +
                   "선택지에 따라 피해·쿨타임·사격 횟수가 달라집니다.",
            FactKind = TermFactKind.ChiefSkill, FactSource = Load<ScriptableObject>(DroneData + "AlphanActiveSkillData.asset"),
            Clip = Load<VideoClip>(ClipRoot + "TermClip_ChiefSkill.mp4"), ClipCaption = "액티브 스킬 — 일제 사격 · 예고 끊기",
        },
        new TermEntry
        {
            Id = "self_destruct_drone", DisplayName = "자폭 드론", Aliases = new[] { "자폭 드론", "자폭드론" },
            Kind = "유닛", KindColor = new Color(0.3f, 0.55f, 0.94f),
            Body = "베탕이 스킬을 채우면 내보내는 소모성 드론입니다. 잠시 힘을 모은 뒤 보스에게 돌진해 폭발하고 사라집니다. " +
                   "선택지로 더 만들어지기도 하며, 폭탄마다 따로 피해와 치명타를 판정합니다.",
            FactKind = TermFactKind.SelfDestruct, FactSource = Load<ScriptableObject>(DroneData + "UnitData_Betan.asset"),
            Clip = Load<VideoClip>(ClipRoot + "TermClip_SelfDestruct.mp4"), ClipCaption = "자폭 드론",
        },
        new TermEntry
        {
            Id = "hack_stack", DisplayName = "해킹스택", Aliases = new[] { "해킹 스택", "해킹스택" },
            Kind = "자원", KindColor = new Color(0.16f, 0.55f, 0.75f),
            Body = "델탕의 해킹으로 보스에게 쌓아 두는 공유 자원입니다. 감망이 스킬로 스택을 소비해 기폭하면 소비한 만큼 추가 피해를 줍니다. " +
                   "같은 보스의 스택은 델탕과 감망이 함께 쓰며, 보스를 처치하면 사라집니다.",
            FactKind = TermFactKind.HackStack, FactSource = Load<ScriptableObject>(DroneData + "DroneHacking.asset"),
        },
        new TermEntry
        {
            Id = "time_manipulation", DisplayName = "시간조작", Aliases = new[] { "시간조작", "시간 조작", "시간 소비", "시간 회복" },
            Kind = "규칙", KindColor = new Color(0.85f, 0.55f, 0.2f),
            Body = "남은 시간을 다루는 효과입니다. 디시그망은 스킬을 쓸 때마다 남은 시간을 바로 회복하고, " +
                   "오버드라이브 효과는 시간을 써서 공격력·공속·합성·해킹 효율 같은 이득을 얻습니다. " +
                   "시간이 저절로 줄어드는 것은 소비가 아니며, 남은 시간이 0초가 되면 바로 패배합니다(시간 금고 효과 제외).",
            FactKind = TermFactKind.TimeRecovery, FactSource = Load<ScriptableObject>(DroneData + "Disigman.asset"),
        },
    };

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogWarning($"[TermGlossary] 없음: {path}");
        return asset;
    }
}
