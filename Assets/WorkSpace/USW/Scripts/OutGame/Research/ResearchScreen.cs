using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 로비 강화 패널과 단독 OutgameUpgrade 씬에서 공유하는 영구 강화 화면.
/// Start에서 트리 데이터와 화면 설정을 읽어 화면 전체를 코드로 만든다:
/// 배경, 스크롤 트리(구성이 2개 이상이면 테스트 버튼으로 전환), 하단 정보 패널, 강화 알림. 탭은 없다 (특별 노드도 한 트리 안).
/// 처음 해 보는 사람을 위해 화면을 열면 추천 노드로 이동하고 그 노드를 선택해 둔다.
/// 하단은 KHJ 아트의 고정 [추천 강화] / [강화] 두 버튼을 사용한다.
/// 추천 강화는 추천 노드를, 강화는 현재 선택한 노드를 올린다.
/// 진행 상황은 트리마다 IResearchSaveStore(지금은 PlayerPrefs)에 따로 저장한다. 영구 강화는 재화를 소모하지 않는다.
/// </summary>
public sealed class ResearchScreen : MonoBehaviour
{
    private const float TopBarHeight = 130f;
    private const float TopButtonWidth = 330f;
    private const float TopButtonHeight = 84f;
    private const float TopButtonFont = 34f;
    private const float Margin = 24f;
    private const float ToastWidth = 760f;
    private const float ToastHeight = 110f;
    private const float ToastFont = 38f;
    private const float ToastRise = 40f;
    private const float ToastFadeSeconds = 0.2f;
    private const float DividerHeight = 2f;

    [Tooltip("구성별 트리. 테스트 버튼을 누를 때마다 다음 구성으로 바뀐다")]
    [SerializeField] private ResearchTreeData[] _trees;
    [SerializeField] private ResearchViewSettings _settings;
    [Tooltip("로비 강화 탭 안에서 표시한다. 로비의 공용 상단/하단 메뉴를 사용한다")]
    [SerializeField] private bool _embeddedInLobby;
    [Tooltip("기존 무버전 PlayerPrefs 저장 이관에만 사용하는 접두사")]
    [SerializeField] private string _saveKeyPrefix = "OutgameUpgrade.";
    [Tooltip("테스트용 [구성 전환] [초기화] 버튼 표시")]
    [SerializeField] private bool _showTestButtons = false;

    private ResearchProgress[] _progresses;
    private ResearchTreeView[] _views;
    private int _current;
    private ResearchInfoPanel _info;
    private ResearchNodeData _selected;
    private TextMeshProUGUI _layoutLabel;
    private RectTransform _toast;
    private CanvasGroup _toastGroup;
    private TextMeshProUGUI _toastText;
    private ResearchSaveService _saveService;
    private ResearchAccountContext _account;
    private ResearchSaveSession[] _sessions;
    private CancellationTokenSource _loading;
    private RectTransform _content;
    private TextMeshProUGUI _status;
    private Button _retry;
    private int _request;
    private SceneChangeManager _scenes;
    private bool _returning;
    private Button _back;
    private TextMeshProUGUI _goldBalance;
    /// <summary>루트에 등록된 저장 서비스와 계정 경계를 주입한다.</summary>
    [Inject]
    public void Construct(ResearchSaveService saveService, ResearchAccountContext account, SceneChangeManager scenes)
    { _saveService = saveService; _account = account; _scenes = scenes; }

    /// <summary>지금 보이는 구성의 진행 상황 (검증·연동용).</summary>
    public ResearchProgress Progress => _progresses?[_current];
    /// <summary>현재 선택된 노드.</summary>
    public ResearchNodeData Selected => _selected;

    /// <summary>로비의 공용 메뉴 사이에 표시하는 화면인가.</summary>
    public bool IsEmbeddedInLobby => _embeddedInLobby;

    /// <summary>트리의 배경 표현만 교체한다.</summary>
    public void SetBackgroundSettings(ResearchViewSettings settings)
    {
        if (_views == null) return;
        foreach (var view in _views) view.SetBackgroundSettings(settings);
    }

    private ResearchTreeView View => _views[_current];

    private void Start()
    {
        if (_saveService == null || _account == null)
        { Debug.LogError("[ResearchScreen] ResearchLifetimeScope의 DI 주입이 필요합니다.", this); return; }
        if (_trees == null || _trees.Length == 0 || _settings == null)
        { Debug.LogError("[ResearchScreen] 트리 데이터 또는 화면 설정 미연결", this); return; }
        var bar = ResearchUi.NewRect(transform, "SaveStatus");
        ResearchUi.Place(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -Margin), new Vector2(650f, TopBarHeight));
        _status = ResearchUi.NewText(bar, "Message", string.Empty, 26f, _settings.FontRegular, TextAlignmentOptions.Center, _settings.FontMaterial);
        _status.color = _settings.Ink;
        _retry = ResearchUi.NewButton(bar, "Retry", _settings.RoundedFill, _settings.Soft, "다시 시도",
            26f, _settings.FontBold, out _, out _, _settings.FontMaterial);
        ResearchUi.Place((RectTransform)_retry.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(180f, 60f));
        _retry.onClick.AddListener(Retry);
        _account.OnAccountChanged += Reload;
        if (!_embeddedInLobby)
        {
            _back = ResearchUi.NewButton(transform, "BackToLobby", _settings.RoundedFill, _settings.Soft, "로비로",
                TopButtonFont, _settings.FontBold, out _, out _, _settings.FontMaterial);
            ResearchUi.Place((RectTransform)_back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -Margin), new Vector2(180f, TopButtonHeight));
            _back.onClick.AddListener(ReturnToLobby);
        }
        Reload();
    }

    private void OnEnable()
    {
        if (Progress != null) RefreshAll();
    }

    private void Reload() => ReloadAsync().Forget();

    private async UniTask ReloadAsync()
    {
        _loading?.Cancel();
        var loading = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        _loading = loading;
        int request = ++_request;
        long generation = _account.Generation;
        string accountKey = _account.AccountKey;
        ReleaseViews();
        _status.text = "강화 정보를 불러오는 중…";
        _retry.gameObject.SetActive(false);
        try
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var sessions = new ResearchSaveSession[_trees.Length];
            for (int i = 0; i < _trees.Length; i++)
            {
                if (_trees[i] == null || !keys.Add(_trees[i].TreeKey)) throw new InvalidOperationException("누락/중복 트리입니다.");
                sessions[i] = _saveService.GetSession(accountKey, _trees[i], _saveKeyPrefix);
                await sessions[i].LoadAsync(loading.Token);
                if (request != _request || generation != _account.Generation) return;
            }
            loading.Token.ThrowIfCancellationRequested();
            _sessions = sessions;
            BuildScreen();
            foreach (var session in _sessions) session.OnSaveStateChanged += UpdateSaveStatus;
            UpdateSaveStatus();
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (request == _request && generation == _account.Generation && this != null)
            {
                _status.text = "강화 정보를 불러오지 못했습니다.";
                _retry.gameObject.SetActive(true);
                Debug.LogWarning("[ResearchScreen] 원본 진행을 보존하고 입력을 대기합니다: " + error.Message);
            }
        }
        finally
        {
            if (_loading == loading) _loading = null;
            loading.Dispose();
        }
    }

    private void Retry()
    {
        if (_progresses == null) { Reload(); return; }
        foreach (var session in _sessions) session.FlushAsync(this.GetCancellationTokenOnDestroy()).Forget();
        UpdateSaveStatus();
    }

    private void UpdateSaveStatus()
    {
        bool failed = false;
        if (_sessions != null) foreach (var session in _sessions) failed |= session.SaveError != null;
        _status.text = failed ? "저장에 실패했습니다. 다시 시도해 주세요." : string.Empty;
        _retry.gameObject.SetActive(failed);
    }

    private void ReleaseViews()
    {
        if (_views != null) foreach (var view in _views) view?.Dispose();
        _views = null;
        if (_sessions != null) foreach (var session in _sessions) session.OnSaveStateChanged -= UpdateSaveStatus;
        if (_progresses != null) foreach (var progress in _progresses) progress.OnChanged -= ProgressChanged;
        _sessions = null;
        _progresses = null;
        _selected = null;
        _current = 0;
        if (_content != null) { _content.gameObject.SetActive(false); Destroy(_content.gameObject); }
        _content = null;
    }

    private void OnDestroy()
    {
        if (_account != null) _account.OnAccountChanged -= Reload;
        _loading?.Cancel();
        ReleaseViews();
    }

    private void ProgressChanged(ResearchNodeData node) => RefreshAll();

    /// <summary>저장을 확인한 뒤 로비로 돌아간다. 실패하면 원본 화면에서 재시도한다.</summary>
    public void ReturnToLobby() => ReturnToLobbyAsync().Forget();

    private async UniTask ReturnToLobbyAsync()
    {
        if (_returning) return;
        _returning = true;
        try
        {
            if (_sessions != null) foreach (var session in _sessions)
            {
                await session.FlushAsync(this.GetCancellationTokenOnDestroy());
                if (session.IsDirty || session.SaveError != null) { UpdateSaveStatus(); return; }
            }
            await _scenes.TransitionToSceneAsync("LobbyScene");
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { Debug.LogWarning("[ResearchScreen] 로비 복귀 실패: " + error.Message); }
        finally { _returning = false; }
    }

    private void BuildScreen()
    {
        if (_trees == null || _trees.Length == 0 || _settings == null) { Debug.LogError("[ResearchScreen] 트리 데이터 또는 화면 설정 미연결", this); return; }

        var s = _settings;
        var root = _content = ResearchUi.Stretch(ResearchUi.NewRect(transform, "Content"));
        _status.transform.parent.SetAsLastSibling();
        if (_back != null) _back.transform.SetAsLastSibling();
        var bg = ResearchUi.NewImage(root, "PanelBg", null, Vector2.zero, Vector2.zero);
        ResearchUi.Stretch(bg.rectTransform);
        bg.color = s.UnfinishedBackground;
        var topBar = ResearchUi.NewImage(root, "TopBar", s.TopBarSprite, Vector2.zero, Vector2.zero);
        topBar.color = s.TopBarSprite != null ? Color.white : s.Surface;
        topBar.rectTransform.anchorMin = new Vector2(0f, 1f);
        topBar.rectTransform.anchorMax = Vector2.one;
        topBar.rectTransform.pivot = new Vector2(0.5f, 1f);
        topBar.rectTransform.sizeDelta = new Vector2(0f, TopBarHeight);
        var title = ResearchUi.NewText(topBar.transform, "Title", "영구 강화", TopButtonFont, s.FontBold, TextAlignmentOptions.Center, s.FontMaterial);
        title.color = s.Ink;

        var wallet = ResearchUi.NewImage(topBar.transform, "GoldBalance", s.RoundedFill, Vector2.zero, Vector2.zero);
        wallet.type = Image.Type.Sliced;
        wallet.color = new Color32(0x00, 0x50, 0x60, 0xFF);
        ResearchUi.Place(wallet.rectTransform, Vector2.one, Vector2.one,
            new Vector2(-Margin, -Margin), new Vector2(260f, 72f));
        _goldBalance = ResearchUi.NewText(wallet.transform, "Amount", "무료", 34f, s.FontBold, TextAlignmentOptions.Center, s.FontMaterial);
        ResearchUi.Stretch(_goldBalance.rectTransform);
        topBar.gameObject.SetActive(!_embeddedInLobby);

        float testBarHeight = _showTestButtons ? Margin * 2f + TopButtonHeight : 0f;
        float headerHeight = _embeddedInLobby ? 0f : TopBarHeight;
        float panelBodyHeight = Mathf.Max(0f, s.PanelHeight - s.PanelTreeOverlap);
        var treeArea = ResearchUi.Stretch(ResearchUi.NewRect(root, "TreeArea"), 0f, panelBodyHeight, 0f, headerHeight + testBarHeight);

        _progresses = new ResearchProgress[_trees.Length];
        _views = new ResearchTreeView[_trees.Length];
        for (int i = 0; i < _trees.Length; i++)
        {
            _progresses[i] = _sessions[i].Progress;
            _progresses[i].OnChanged += ProgressChanged;
            _views[i] = new ResearchTreeView(treeArea, _trees[i], s, _progresses[i]);
            _views[i].OnNodeClicked += Select;
        }

        var panel = ResearchUi.NewImage(root, "InfoPanel", s.CardBackground, Vector2.zero, Vector2.zero);
        panel.color = s.CardBackground != null ? Color.white : s.Surface;
        var panelRect = panel.rectTransform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(0f, s.PanelHeight);
        // 트리와 패널 사이 가는 구분선.
        var divider = ResearchUi.NewImage(panelRect, "Divider", null, Vector2.zero, Vector2.zero);
        divider.color = s.CardBackground != null ? Color.clear : s.Faint;
        var dividerRect = divider.rectTransform;
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.sizeDelta = new Vector2(0f, DividerHeight);
        _info = new ResearchInfoPanel(panelRect, s);
        _info.OnUpgradeClicked += Upgrade;
        _info.OnRecommendClicked += UpgradeRecommended;
        _info.OnChangeChoiceClicked += ChangeChoice;

        if (_showTestButtons) BuildTestButtons(root);
        BuildToast(root);

        ShowTree(0);
    }

    /// <summary>노드를 선택하고 하단 패널에 보여준다.</summary>
    public void Select(ResearchNodeData node)
    {
        _selected = node;
        View.Select(node);
        _info.Show(node);
    }

    /// <summary>노드를 한 단계 올린다 ([강화] 버튼).</summary>
    public void Upgrade(ResearchNodeData node)
    {
        if (Progress == null || !Progress.CanUpgrade(node)) return;
        if (!Progress.TryUpgrade(node))
        {
            RefreshAll();
            return;
        }
        View.Punch(node);
        _info.Punch();
        ShowToast(node);
    }

    /// <summary>추천 노드로 이동·선택하고 한 단계 올린다.</summary>
    public void UpgradeRecommended()
    {
        if (Progress == null) return;
        var node = Progress.GetRecommended();
        if (node == null) return;
        Select(node);
        View.ScrollTo(node, true);
        Upgrade(node);
    }

    /// <summary>택1 갈림길에서 고른 쪽을 되돌리고 이 노드를 고를 수 있게 한다.</summary>
    public void ChangeChoice(ResearchNodeData node)
    {
        if (Progress != null && Progress.ResetChoice(node)) Select(node);
    }

    /// <summary>다음 구성으로 바꾼다 (테스트 버튼).</summary>
    public void NextLayout() => ShowTree((_current + 1) % _trees.Length);

    private void ShowTree(int index)
    {
        _current = index;
        for (int i = 0; i < _views.Length; i++) _views[i].Root.SetActive(i == index);
        _info.SetProgress(Progress);
        if (_layoutLabel != null)
            _layoutLabel.text = string.Format(_settings.LayoutButtonFormat, _trees[index].DisplayName);
        var start = Progress.GetRecommended() ?? (_trees[index].Nodes.Count > 0 ? _trees[index].Nodes[0] : null);
        Select(start);
        View.ScrollTo(start);
    }

    private void RefreshAll()
    {
        _goldBalance.text = "무료";
        View.Refresh();
        _info.Refresh();
    }

    private void BuildTestButtons(RectTransform root)
    {
        var s = _settings;
        if (_trees.Length > 1)
        {
            var layout = ResearchUi.NewButton(root, "LayoutButton", s.RoundedFill, s.Soft, string.Empty,
                TopButtonFont, s.FontBold, out var layoutImage, out _layoutLabel, s.FontMaterial);
            layoutImage.pixelsPerUnitMultiplier = s.RoundedCornerScale;
            _layoutLabel.color = s.Ink;
            ResearchUi.Place((RectTransform)layout.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(Margin, -TopBarHeight - Margin), new Vector2(TopButtonWidth * 1.4f, TopButtonHeight));
            layout.onClick.AddListener(NextLayout);
        }

        var reset = ResearchUi.NewButton(root, "ResetButton", s.RoundedFill, s.Soft, s.ResetLabel,
            TopButtonFont, s.FontBold, out var resetImage, out var resetText, s.FontMaterial);
        resetImage.pixelsPerUnitMultiplier = s.RoundedCornerScale;
        resetText.color = s.Muted;
        ResearchUi.Place((RectTransform)reset.transform, Vector2.one, Vector2.one, new Vector2(-Margin, -TopBarHeight - Margin),
            new Vector2(TopButtonWidth * 0.6f, TopButtonHeight));
        reset.onClick.AddListener(() =>
        {
            Progress.ResetAll();
            ShowTree(_current);
        });

    }

    private void BuildToast(RectTransform root)
    {
        var s = _settings;
        var bg = ResearchUi.NewImage(root, "Toast", s.RoundedFill, new Vector2(ToastWidth, ToastHeight), Vector2.zero);
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = s.RoundedCornerScale;
        bg.color = s.Soft;
        _toast = bg.rectTransform;
        _toast.anchorMin = _toast.anchorMax = new Vector2(0.5f, 1f);
        _toast.pivot = new Vector2(0.5f, 1f);
        _toastGroup = bg.gameObject.AddComponent<CanvasGroup>();
        _toastGroup.blocksRaycasts = false;
        _toastText = ResearchUi.NewText(bg.transform, "Text", string.Empty, ToastFont, s.FontBold, TextAlignmentOptions.Center, s.FontMaterial);
        _toastText.color = s.Ink;
        _toast.gameObject.SetActive(false);
    }

    // "공격력 증폭 Lv.2  +4%" 알림이 위에서 살짝 내려왔다가 사라진다.
    private void ShowToast(ResearchNodeData node)
    {
        var s = _settings;
        int level = Progress.GetLevel(node);
        _toastText.text = string.Format(s.ToastFormat, s.GetDisplayName(Progress.Tree, node), level,
            s.FormatDisplayValue(Progress.Tree, node, level));

        _toast.DOKill();
        _toastGroup.DOKill();
        _toast.gameObject.SetActive(true);
        float y = -TopBarHeight - Margin;
        _toast.anchoredPosition = new Vector2(0f, y + ToastRise);
        _toastGroup.alpha = 0f;
        DOTween.Sequence()
            .Append(_toast.DOAnchorPosY(y, ToastFadeSeconds).SetEase(Ease.OutBack))
            .Join(_toastGroup.DOFade(1f, ToastFadeSeconds))
            .AppendInterval(s.ToastSeconds)
            .Append(_toastGroup.DOFade(0f, ToastFadeSeconds))
            .OnComplete(() => _toast.gameObject.SetActive(false))
            .SetLink(_toast.gameObject)
            .SetTarget(_toast);
    }
}
