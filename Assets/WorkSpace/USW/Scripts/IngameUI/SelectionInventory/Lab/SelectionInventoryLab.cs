using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GaeGGUL.UI.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 획득 선택지 보관함 실험실(FxLab_SelectionInventory). 실제 프리팹(SelectionInventory.prefab)을 표본 카드로 띄운다.
/// 1차 시안 A/B/C 중 사용자 A안 선택(10-13) → 프리팹화. 표본은 획득 순서 3/8/24장.
/// 9121~9124는 SO Feedback이 비어 있어 실험실에서만 설명 문구 기준으로 대상 유닛을 보정한다.
/// </summary>
public sealed class SelectionInventoryLab : MonoBehaviour
{
    /// <summary>표본 크기(획득 장수) 이름.</summary>
    public static readonly string[] SampleLabels = { "3장(초반)", "8장(중반)", "24장(전부)", "0장" };

    private static readonly int[] EarlySample = { 9101, 9107, 9110 };
    private static readonly int[] MidSample = { 9101, 9107, 9110, 9106, 9108, 9118, 9114, 9120 };

    // Feedback 미기입 카드 — 실험실 임시 보정 (실제 적용 시 SO Feedback에 기입 필요)
    private static readonly Dictionary<int, SelectionFeedbackTarget> PatchedTargets = new()
    {
        { 9121, SelectionFeedbackTarget.Deltan }, { 9122, SelectionFeedbackTarget.Betan },
        { 9123, SelectionFeedbackTarget.Deltan }, { 9124, SelectionFeedbackTarget.Deltan },
    };

    private static readonly Color ButtonIdle = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color ButtonSelected = new Color(0.35f, 0.8f, 0.45f, 0.9f);

    [SerializeField] private SelectionInventoryUI _inventory;
    [SerializeField] private UI_UnitInfoPanel _infoPanel;
    [SerializeField] private LevelUpData[] _cards = Array.Empty<LevelUpData>();
    [SerializeField] private RectTransform _controls;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Sprite _rounded;
    [SerializeField] private int _sample = 1;

    private readonly Dictionary<int, SelectionInventoryItem> _items = new();
    private readonly List<Image> _sampleButtons = new();

    private void Start()
    {
        var state = new LevelUpRunState();
        state.BeginRun(GetInstanceID());
        var formatter = new SelectionDescriptionFormatter();
        foreach (var card in _cards)
        {
            if (card == null) continue;
            DescriptionSnapshot snapshot = null;
            try { snapshot = formatter.Capture(state.Prepare(card, UnityEngine.Random.Range), card.simpleDescription); }
            catch (Exception ex) { Debug.LogWarning($"[SelectionInventoryLab] {card.chooseId} 설명 계산 실패: {ex.Message}"); }
            SelectionFeedbackTarget? patched = PatchedTargets.TryGetValue(card.chooseId, out var t) ? t : null;
            _items[card.chooseId] = new SelectionInventoryItem(card, snapshot, patched);
        }
        if (_infoPanel != null)
        {
            _infoPanel.OnDismissRequested += _infoPanel.Close; // 게임에서는 InGameInstaller가 같은 일을 한다
            _infoPanel.gameObject.SetActive(false);
        }
        BuildControls();
        Open();
    }

    /// <summary>표본 크기 선택: 0 초반, 1 중반, 2 전부, 3 빈 목록.</summary>
    public void SelectSample(int index)
    {
        _sample = Mathf.Clamp(index, 0, SampleLabels.Length - 1);
        Open();
    }

    /// <summary>현재 표본으로 보관함을 연다 (≡ → 선택지와 같은 경로).</summary>
    public void Open()
    {
        for (int i = 0; i < _sampleButtons.Count; i++) _sampleButtons[i].color = i == _sample ? ButtonSelected : ButtonIdle;
        _inventory.Show(Sample());
    }

    /// <summary>시안 표본·채움 연출·용어 시트·정보창을 PNG로 저장한다.</summary>
    public void CaptureAll(string folder) => StartCoroutine(CaptureRoutine(folder));

    private List<SelectionInventoryItem> Sample()
    {
        var list = new List<SelectionInventoryItem>();
        if (_sample == 3) return list;
        if (_sample == 2)
        {
            foreach (var card in _cards) if (card != null && _items.TryGetValue(card.chooseId, out var item)) list.Add(item);
            return list;
        }
        foreach (int id in _sample == 0 ? EarlySample : MidSample)
            if (_items.TryGetValue(id, out var item)) list.Add(item);
        return list;
    }

    private void BuildControls()
    {
        var layout = _controls.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        for (int i = 0; i < SampleLabels.Length; i++)
        {
            int index = i;
            _sampleButtons.Add(Button(SampleLabels[i], () => SelectSample(index)));
        }
        Button("닫기", _inventory.Close);
    }

    private Image Button(string label, Action onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_controls, false);
        var image = go.GetComponent<Image>();
        image.sprite = _rounded;
        image.type = Image.Type.Sliced;
        image.color = ButtonIdle;
        go.GetComponent<Button>().onClick.AddListener(() => onClick());
        var text = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(go.transform, false);
        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        text.font = _font;
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = label;
        return image;
    }

    private IEnumerator CaptureRoutine(string folder)
    {
        Directory.CreateDirectory(folder);
        for (int n = 0; n < SampleLabels.Length; n++)
        {
            SelectSample(n);
            yield return new WaitForSecondsRealtime(1.3f);
            yield return new WaitForEndOfFrame();
            Save(Path.Combine(folder, $"P_{n}_{SampleLabels[n]}.png"));
        }

        SelectSample(2);
        yield return new WaitForSecondsRealtime(0.14f + 0.6f * 0.45f);
        yield return new WaitForEndOfFrame();
        Save(Path.Combine(folder, "P_fill_mid.png"));

        // 용어: 영상(해킹) / 글자(식량). 칸 누르기와 같은 경로를 쓰려고 실제 칸 버튼을 누른다.
        SelectSample(1);
        yield return new WaitForSecondsRealtime(1.2f);
        Click(9107);
        yield return new WaitForSecondsRealtime(0.4f);
        _inventory.ShowTerm("hack_stack");
        yield return new WaitForSecondsRealtime(1.6f);
        yield return new WaitForEndOfFrame();
        Save(Path.Combine(folder, "P_term_video.png"));
        _inventory.TermPopup?.HideImmediately();
        Click(9106);
        yield return new WaitForSecondsRealtime(0.4f);
        _inventory.ShowTerm("food");
        yield return new WaitForSecondsRealtime(0.8f);
        yield return new WaitForEndOfFrame();
        Save(Path.Combine(folder, "P_term_text.png"));
        _inventory.TermPopup?.HideImmediately();

        // 대상 칩 → 정보창 / 족장 카드
        Click(9108);
        yield return new WaitForSecondsRealtime(0.4f);
        ClickChip(0);
        yield return new WaitForSecondsRealtime(0.8f);
        yield return new WaitForEndOfFrame();
        Save(Path.Combine(folder, "P_info_deltan.png"));
        _infoPanel?.Close();
        yield return new WaitForSecondsRealtime(0.4f);
        Click(9114);
        yield return new WaitForSecondsRealtime(0.4f);
        ClickChip(0);
        yield return new WaitForSecondsRealtime(0.5f);
        yield return new WaitForEndOfFrame();
        Save(Path.Combine(folder, "P_chief_card.png"));
        Debug.Log($"[SelectionInventoryLab] 캡처 완료: {folder}");
    }

    private void Click(int chooseId)
    {
        foreach (var slot in _inventory.GetComponentsInChildren<SelectionInventorySlot>())
            if (slot.Item != null && slot.Item.Card.chooseId == chooseId) { slot.GetComponent<Button>().onClick.Invoke(); return; }
    }

    private void ClickChip(int index)
    {
        var chips = _inventory.transform.Find("Window/Panel/Detail/Content/Chips");
        int seen = 0;
        foreach (Transform chip in chips)
        {
            if (!chip.gameObject.activeSelf) continue;
            if (seen++ == index) { chip.GetComponent<Button>().onClick.Invoke(); return; }
        }
    }

    private static void Save(string path)
    {
        var shot = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(path, shot.EncodeToPNG());
        Destroy(shot);
    }
}
