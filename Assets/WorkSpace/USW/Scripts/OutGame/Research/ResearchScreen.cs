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
/// 아웃게임 강화(영구 강화) 화면 진입점 — OutgameUpgrade 씬. (코드 이름은 Research, 화면 문구는 "강화")
/// Start에서 트리 데이터와 화면 설정을 읽어 화면 전체를 코드로 만든다:
/// 배경, 스크롤 트리(구성이 2개 이상이면 테스트 버튼으로 전환), 하단 정보 패널, 강화 알림. 탭은 없다 (특별 노드도 한 트리 안).
/// 처음 해 보는 사람을 위해 화면을 열면 추천 노드로 이동하고 그 노드를 선택해 둔다.
/// 하단 패널을 좌우로 밀어 추천 방식 두 가지를 비교한다: 0번 [추천 강화] 버튼, 1번 패널 오른쪽 위 [추천 따라가기] 토글(켜면 [강화]가 추천 노드를 올림).
/// 두 방식 모두 지금 선택과 상관없이 추천 노드로 이동해 올린다 (6-1을 보고 있어도 추천이 7-2면 7-2).
/// 진행 상황은 트리마다 IResearchSaveStore(지금은 PlayerPrefs)에 따로 저장한다. 비용은 아직 없다.
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
    [Tooltip("기존 무버전 PlayerPrefs 저장 이관에만 사용하는 접두사")]
    [SerializeField] private string _saveKeyPrefix = "OutgameUpgrade.";
    [Tooltip("[추천 따라가기] 켜짐 여부를 저장하는 PlayerPrefs 키")]
    [SerializeField] private string _followKey = "OutgameUpgrade.FollowRecommend";
    [Tooltip("마지막으로 쓴 추천 방식(0 버튼형 / 1 토글형)을 저장하는 PlayerPrefs 키")]
    [SerializeField] private string _modeKey = "OutgameUpgrade.RecommendMode";
    [Tooltip("테스트용 [구성 전환] [초기화] 버튼 표시")]
    [SerializeField] private bool _showTestButtons = true;

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

    /// <summary>루트에 등록된 저장 서비스와 계정 경계를 주입한다.</summary>
    [Inject]
    public void Construct(ResearchSaveService saveService, ResearchAccountContext account)
    { _saveService = saveService; _account = account; }

    /// <summary>지금 보이는 구성의 진행 상황 (검증·연동용).</summary>
    public ResearchProgress Progress => _progresses?[_current];
    /// <summary>현재 선택된 노드.</summary>
    public ResearchNodeData Selected => _selected;

    private ResearchTreeView View => _views[_current];

    private void Start()
    {
        if (_saveService == null || _account == null)
        { Debug.LogError("[ResearchScreen] ResearchLifetimeScope의 DI 주입이 필요합니다.", this); return; }
        if (_trees == null || _trees.Length == 0 || _settings == null)
        { Debug.LogError("[ResearchScreen] 트리 데이터 또는 화면 설정 미연결", this); return; }
        var bar = ResearchUi.NewRect(transform, "SaveStatus");
        ResearchUi.Place(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -Margin), new Vector2(650f, TopBarHeight));
        _status = ResearchUi.NewText(bar, "Message", string.Empty, 26f, _settings.FontRegular, TextAlignmentOptions.Center);
        _status.color = _settings.Ink;
        _retry = ResearchUi.NewButton(bar, "Retry", _settings.RoundedFill, _settings.Soft, "다시 시도",
            26f, _settings.FontBold, out _, out _);
        ResearchUi.Place((RectTransform)_retry.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(180f, 60f));
        _retry.onClick.AddListener(Retry);
        _account.OnAccountChanged += Reload;
        Reload();
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

    private void BuildScreen()
    {
        if (_trees == null || _trees.Length == 0 || _settings == null) { Debug.LogError("[ResearchScreen] 트리 데이터 또는 화면 설정 미연결", this); return; }

        var s = _settings;
        var root = _content = ResearchUi.Stretch(ResearchUi.NewRect(transform, "Content"));
        _status.transform.parent.SetAsLastSibling();
        var bg = ResearchUi.NewImage(root, "Background", null, Vector2.zero, Vector2.zero);
        ResearchUi.Stretch(bg.rectTransform);
        bg.color = s.Background;

        var treeArea = ResearchUi.Stretch(ResearchUi.NewRect(root, "TreeArea"), 0f, s.PanelHeight, 0f, TopBarHeight);

        _progresses = new ResearchProgress[_trees.Length];
        _views = new ResearchTreeView[_trees.Length];
        for (int i = 0; i < _trees.Length; i++)
        {
            _progresses[i] = _sessions[i].Progress;
            _progresses[i].OnChanged += ProgressChanged;
            _views[i] = new ResearchTreeView(treeArea, _trees[i], s, _progresses[i]);
            _views[i].OnNodeClicked += Select;
        }

        var panel = ResearchUi.NewImage(root, "InfoPanel", null, Vector2.zero, Vector2.zero);
        panel.color = s.Surface;
        var panelRect = panel.rectTransform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(0f, s.PanelHeight);
        // 트리와 패널 사이 가는 구분선.
        var divider = ResearchUi.NewImage(panelRect, "Divider", null, Vector2.zero, Vector2.zero);
        divider.color = s.Faint;
        var dividerRect = divider.rectTransform;
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.sizeDelta = new Vector2(0f, DividerHeight);
        _info = new ResearchInfoPanel(panelRect, s);
        _info.OnUpgradeClicked += Upgrade;
        _info.OnRecommendClicked += UpgradeRecommended;
        _info.OnFollowToggled += SetFollow;
        _info.OnModeChanged += mode => { PlayerPrefs.SetInt(_modeKey, mode); PlayerPrefs.Save(); };
        _info.SetFollow(PlayerPrefs.GetInt(_followKey, s.FollowByDefault ? 1 : 0) == 1);
        _info.SetMode(PlayerPrefs.GetInt(_modeKey, 0));
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
        if (Progress == null || !Progress.TryUpgrade(node)) return;
        View.Punch(node);
        _info.Punch();
        ShowToast(node);
    }

    /// <summary>[추천 따라가기] 켜기/끄기 (켜면 토글형 [강화]가 추천 노드를 올린다). 켜면 바로 추천 노드로 이동한다. 저장한다.</summary>
    public void SetFollow(bool on)
    {
        PlayerPrefs.SetInt(_followKey, on ? 1 : 0);
        PlayerPrefs.Save();
        _info.SetFollow(on);
        if (!on) return;
        var next = Progress.GetRecommended();
        if (next == null) return;
        Select(next);
        View.ScrollTo(next, true);
    }

    /// <summary>검증용: 지금 추천 방식 (0 버튼형 / 1 토글형).</summary>
    public int RecommendMode => _info.Mode;

    /// <summary>검증용: 스와이프와 같은 경로로 추천 방식을 넘긴다 (+1 다음, -1 이전).</summary>
    public void SwipeMode(int direction) => _info.SetMode(_info.Mode + direction);

    /// <summary>추천 노드로 이동·선택하고 한 단계 올린다 (0번 [추천 강화], 1번 토글 켜짐 + [강화]).</summary>
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
        View.Refresh();
        _info.Refresh();
    }

    private void BuildTestButtons(RectTransform root)
    {
        var s = _settings;
        if (_trees.Length > 1)
        {
            var layout = ResearchUi.NewButton(root, "LayoutButton", s.RoundedFill, s.Soft, string.Empty,
                TopButtonFont, s.FontBold, out var layoutImage, out _layoutLabel);
            layoutImage.pixelsPerUnitMultiplier = s.RoundedCornerScale;
            _layoutLabel.color = s.Ink;
            ResearchUi.Place((RectTransform)layout.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -Margin), new Vector2(TopButtonWidth * 1.4f, TopButtonHeight));
            layout.onClick.AddListener(NextLayout);
        }

        var reset = ResearchUi.NewButton(root, "ResetButton", s.RoundedFill, s.Soft, s.ResetLabel,
            TopButtonFont, s.FontBold, out var resetImage, out var resetText);
        resetImage.pixelsPerUnitMultiplier = s.RoundedCornerScale;
        resetText.color = s.Muted;
        ResearchUi.Place((RectTransform)reset.transform, Vector2.one, Vector2.one, new Vector2(-Margin, -Margin),
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
        _toastText = ResearchUi.NewText(bg.transform, "Text", string.Empty, ToastFont, s.FontBold, TextAlignmentOptions.Center);
        _toastText.color = s.Ink;
        _toast.gameObject.SetActive(false);
    }

    // "공격력 증폭 Lv.2  +4%" 알림이 위에서 살짝 내려왔다가 사라진다.
    private void ShowToast(ResearchNodeData node)
    {
        var s = _settings;
        int level = Progress.GetLevel(node);
        _toastText.text = string.Format(s.ToastFormat, s.GetStat(node.Stat).DisplayName, level,
            s.FormatValue(node.Stat, ResearchProgress.GetValue(node, level)));

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
