using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// SkillData를 목록에서 골라 편집하는 툴 (기획자용).
///
/// InspectorElement로 실제 에디터를 그대로 임베드하기 때문에 인스펙터(Alchemy 포함)와 동일하게
/// 보인다 — TotemEditorWindow/BuffRangeEditorWindow처럼 필드를 손으로 그리지 않는다.
///
/// action(SerializeReference, 예: MultiShotSkillAction)이 ProjectileData/BuffData 같은
/// ScriptableObject 참조 필드를 갖고 있으면, 그 SO도 바로 아래에 같은 방식(InspectorElement)으로
/// 편집할 수 있게 보여준다. 값이 비어있으면 이름 입력 + "생성하기" 버튼을 대신 보여주고, 누르면
/// HSD/Data/... 의 정해진 경로에 새 에셋을 만들어 그 자리에 바로 연결한다.
/// </summary>
public class SkillEditorWindow : EditorWindow
{
    /// <summary>새로 만들 SO 타입별 기본 폴더/이니셜. 새 타입이 필요해지면 여기에 추가하면 된다.</summary>
    private static readonly Dictionary<Type, (string folder, string prefix)> KnownSoTypes = new()
    {
        { typeof(ProjectileData), ("Assets/WorkSpace/HSD/Data/Projectile", "PD") },
        { typeof(BuffData), ("Assets/WorkSpace/HSD/Data/Buff", "BD") },
    };

    [MenuItem("Tools/GGD_Editor/Skill Editor", false, 0)]
    public static void Open()
    {
        var win = GetWindow<SkillEditorWindow>("Skill Editor");
        win.minSize = new Vector2(420f, 520f);
        win.position = new Rect(win.position.x, win.position.y, 720f, 860f);
    }

    private SkillData _selected;
    private string _search = "";
    private ScrollView _listBox;
    private VisualElement _detail;

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.paddingLeft = 6;
        root.style.paddingRight = 6;
        root.style.paddingTop = 6;
        root.style.paddingBottom = 6;

        var header = new Label("Skill Editor");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.fontSize = 14;
        root.Add(header);

        var searchRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        var searchField = new TextField("검색") { style = { flexGrow = 1 } };
        searchField.RegisterValueChangedCallback(e => { _search = e.newValue; RefreshList(); });
        searchRow.Add(searchField);
        var refreshBtn = new Button(RefreshList) { text = "새로고침", style = { marginLeft = 4 } };
        searchRow.Add(refreshBtn);
        root.Add(searchRow);

        var pickField = new ObjectField("직접 드래그")
        {
            objectType = typeof(SkillData),
            allowSceneObjects = false
        };
        pickField.RegisterValueChangedCallback(e => Select(e.newValue as SkillData));
        root.Add(pickField);

        _listBox = new ScrollView(ScrollViewMode.Vertical);
        _listBox.style.maxHeight = 160;
        _listBox.style.marginTop = 4;
        _listBox.style.marginBottom = 4;
        _listBox.style.borderTopWidth = 1;
        _listBox.style.borderBottomWidth = 1;
        _listBox.style.borderTopColor = new Color(0.35f, 0.35f, 0.35f);
        _listBox.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f);
        root.Add(_listBox);

        root.Add(Separator());

        var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
        _detail = scroll.contentContainer;
        root.Add(scroll);

        RefreshList();
    }

    // ── 목록 ──────────────────────────────────────────────────────
    private void RefreshList()
    {
        _listBox.Clear();

        var guids = AssetDatabase.FindAssets("t:SkillData");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if (data == null) continue;

            if (!string.IsNullOrEmpty(_search) &&
                data.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0 &&
                (data.skillName ?? "").IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var btn = new Button(() => Select(data)) { text = $"{data.name}  ({data.skillName})" };
            if (_selected == data)
                btn.style.backgroundColor = new Color(0.24f, 0.35f, 0.55f);
            _listBox.Add(btn);
        }
    }

    private void Select(SkillData data)
    {
        _selected = data;
        RefreshList();
        RebuildDetail();
    }

    // ── 상세(인스펙터 임베드) ────────────────────────────────────
    private void RebuildDetail()
    {
        // 한 VisualElement는 동시에 하나의 SerializedObject만 추적할 수 있어서, 다른 스킬을
        // 다시 선택할 때 이전 추적을 풀어주지 않으면 TrackSerializedObjectValue가 예외를 던진다.
        _detail.Unbind();
        _detail.Clear();
        if (_selected == null) return;

        var soLabel = new Label("SkillData");
        soLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _detail.Add(soLabel);

        var so = new SerializedObject(_selected);
        _detail.Add(new InspectorElement(_selected));

        // action이나 그 하위 값 중 무엇이 바뀌든(타입 교체, 필드 재할당 등) 하단 SO 편집기 갱신.
        _detail.TrackSerializedObjectValue(so, _ => RebuildNestedEditors());

        var nestedContainer = new VisualElement { name = "nested-container" };
        _detail.Add(nestedContainer);

        RebuildNestedEditors();
    }

    private void RebuildNestedEditors()
    {
        if (_selected == null) return;
        var container = _detail.Q<VisualElement>("nested-container");
        if (container == null) return;
        container.Clear();

        var action = _selected.action;
        if (action == null) return;

        foreach (var field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (typeof(ScriptableObject).IsAssignableFrom(field.FieldType))
            {
                BuildDirectReferenceUI(container, field, action);
                continue;
            }

            var idRef = field.GetCustomAttribute<SoIdReferenceAttribute>();
            if (idRef != null && field.FieldType == typeof(int))
            {
                BuildIdReferenceUI(container, idRef.SoType, field, action);
            }
        }
    }

    // ── 직접 참조(SerializeReference/ScriptableObject 필드) 편집 ───
    private void BuildDirectReferenceUI(VisualElement container, FieldInfo field, object action)
    {
        container.Add(Separator());

        var label = new Label($"{ObjectNames.NicifyVariableName(field.Name)} ({field.FieldType.Name})");
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        container.Add(label);

        var value = field.GetValue(action) as ScriptableObject;
        if (value != null)
        {
            container.Add(new InspectorElement(value));
            return;
        }

        container.Add(new HelpBox($"연결된 {field.FieldType.Name}가 없습니다.", HelpBoxMessageType.Info));

        var nameField = new TextField("생성할 이름") { value = ResolveDefaultName(field.FieldType, _selected) };
        container.Add(nameField);

        container.Add(new Button(() =>
        {
            CreateAndAssign(field.FieldType, nameField.value, action, field, _selected);
            RebuildNestedEditors();
        })
        { text = "생성하기" });
    }

    // ── id 참조(SoIdReference 붙은 int 필드) 편집 ──────────────────
    private void BuildIdReferenceUI(VisualElement container, Type soType, FieldInfo field, object action)
    {
        container.Add(Separator());

        var label = new Label($"{ObjectNames.NicifyVariableName(field.Name)} ({soType.Name}, id 참조)");
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        container.Add(label);

        int currentId = (int)field.GetValue(action);
        var candidates = FindAllOfType(soType); // (id, name, asset)

        var idField = new IntegerField("id") { value = currentId };
        idField.RegisterValueChangedCallback(e =>
        {
            Undo.RecordObject(_selected, $"Set {field.Name}");
            field.SetValue(action, e.newValue);
            EditorUtility.SetDirty(_selected);
            RebuildNestedEditors();
        });
        container.Add(idField);

        var options = new List<string> { "(없음)" };
        options.AddRange(candidates.Select(c => $"{c.id}: {c.asset.name}"));
        int selectedIndex = currentId == 0 ? 0 : candidates.FindIndex(c => c.id == currentId) + 1;
        var popup = new PopupField<string>("목록에서 선택", options, Mathf.Max(selectedIndex, 0));
        popup.RegisterValueChangedCallback(e =>
        {
            int newIndex = options.IndexOf(e.newValue);
            int newId = newIndex <= 0 ? 0 : candidates[newIndex - 1].id;
            if (newId == currentId) return;
            Undo.RecordObject(_selected, $"Set {field.Name}");
            field.SetValue(action, newId);
            EditorUtility.SetDirty(_selected);
            RebuildNestedEditors();
        });
        container.Add(popup);

        var resolved = candidates.Find(c => c.id == currentId).asset;
        if (currentId != 0 && resolved != null)
        {
            container.Add(new InspectorElement(resolved));
            return;
        }

        if (currentId != 0 && resolved == null)
            container.Add(new HelpBox($"id {currentId}에 해당하는 {soType.Name}를 찾을 수 없습니다.", HelpBoxMessageType.Warning));
        else
            container.Add(new HelpBox($"연결된 {soType.Name}가 없습니다(id 0 = 기본값 사용).", HelpBoxMessageType.Info));

        var nameField = new TextField("생성할 이름") { value = ResolveDefaultName(soType, _selected) };
        container.Add(nameField);

        container.Add(new Button(() =>
        {
            CreateAndAssignId(soType, nameField.value, action, field, _selected, candidates);
            RebuildNestedEditors();
        })
        { text = "생성하기" });
    }

    /// <summary>프로젝트 내 soType 에셋을 모두 찾아 (id, name, asset) 목록으로 반환. soType은 public int id 필드를 가져야 한다.</summary>
    private static List<(int id, string name, ScriptableObject asset)> FindAllOfType(Type soType)
    {
        var result = new List<(int, string, ScriptableObject)>();
        var idField = soType.GetField("id", BindingFlags.Public | BindingFlags.Instance);
        if (idField == null) return result;

        foreach (var guid in AssetDatabase.FindAssets($"t:{soType.Name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath(path, soType) as ScriptableObject;
            if (asset == null) continue;
            result.Add(((int)idField.GetValue(asset), asset.name, asset));
        }
        return result;
    }

    // ── 생성 ──────────────────────────────────────────────────────
    private static string ResolveDefaultName(Type soType, SkillData context)
    {
        string prefix = KnownSoTypes.TryGetValue(soType, out var info) ? info.prefix : soType.Name;
        int underscore = context.name.IndexOf('_');
        string suffix = underscore >= 0 ? context.name.Substring(underscore) : "_" + context.name;
        return prefix + suffix;
    }

    private static void CreateAndAssign(Type soType, string assetName, object owner, FieldInfo field, SkillData contextAsset)
    {
        string folder = KnownSoTypes.TryGetValue(soType, out var info)
            ? info.folder
            : $"Assets/WorkSpace/HSD/Data/{soType.Name}";
        EnsureFolder(folder);

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");
        var instance = ScriptableObject.CreateInstance(soType);
        AssetDatabase.CreateAsset(instance, path);

        Undo.RecordObject(contextAsset, $"Create {soType.Name}");
        field.SetValue(owner, instance);
        EditorUtility.SetDirty(contextAsset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SkillEditor] '{path}' 생성 및 연결 완료.");
    }

    /// <summary>새 soType 에셋을 만들고 기존 id들과 겹치지 않는 새 id를 부여한 뒤, idField(int)에 그 id를 대입한다.</summary>
    private static void CreateAndAssignId(Type soType, string assetName, object owner, FieldInfo idField, SkillData contextAsset,
        List<(int id, string name, ScriptableObject asset)> existing)
    {
        string folder = KnownSoTypes.TryGetValue(soType, out var info)
            ? info.folder
            : $"Assets/WorkSpace/HSD/Data/{soType.Name}";
        EnsureFolder(folder);

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");
        var instance = ScriptableObject.CreateInstance(soType);

        int newId = (existing.Count == 0 ? 0 : existing.Max(e => e.id)) + 1;
        soType.GetField("id", BindingFlags.Public | BindingFlags.Instance).SetValue(instance, newId);

        AssetDatabase.CreateAsset(instance, path);

        Undo.RecordObject(contextAsset, $"Create {soType.Name}");
        idField.SetValue(owner, newId);
        EditorUtility.SetDirty(contextAsset);
        EditorUtility.SetDirty(instance);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SkillEditor] '{path}' 생성(id: {newId}) 및 연결 완료. Addressables 라벨은 Tools/Antigravity/Setup SO Addressables로 등록해야 런타임 조회가 가능합니다.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static VisualElement Separator()
    {
        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
        sep.style.marginTop = 8;
        sep.style.marginBottom = 6;
        return sep;
    }
}
