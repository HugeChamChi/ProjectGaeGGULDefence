using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 레벨업 카드를 꾹 눌러 필드보기 중일 때 그 카드의 대상을 강조한다 (LevelUpUI와 같은 GameObject에 부착).
///   대상 유닛: 제자리에서 통통 튀기 + 발밑 링 / 대상 아닌 유닛: 어둡게 / 알팡 카드: 족장 스킬 버튼 두근.
/// 대상 판별은 LevelUpFeedbackTargets. Clear 시 위치/색/크기를 모두 원래대로 되돌린다.
/// </summary>
public class LevelUpPeekHighlighter : MonoBehaviour
{
    [Inject] private GridManager _gridManager;
    [Inject] private ChieftainSpawner _chieftainSpawner;

    [Header("참조")]
    [Tooltip("링을 그릴 HUD Canvas. 비우면 족장 스킬 버튼이 속한 최상위 Canvas.")]
    [SerializeField] private RectTransform _hudRoot;
    [Tooltip("알팡 카드일 때 두근거릴 족장 스킬 버튼.")]
    [SerializeField] private RectTransform _chiefSkillTarget;

    [Header("강조")]
    [Tooltip("대상이 아닌 유닛을 어둡게 할 색 (스프라이트 색에 곱해짐).")]
    [SerializeField] private Color _dimColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    [Tooltip("대상 유닛이 통통 튀는 높이 (월드 단위).")]
    [SerializeField] private float _hopHeight = 0.15f;
    [SerializeField, Min(0.05f)] private float _hopDuration = 0.3f;
    [Tooltip("대상 유닛 발밑 링 크기 (Canvas 단위).")]
    [SerializeField, Min(1f)] private float _ringSize = 170f;
    [SerializeField] private Color _ringColor = new Color(1f, 0.85f, 0.35f, 0.9f);
    [Tooltip("유닛 피벗 기준 링 높이 (월드 단위).")]
    [SerializeField] private float _ringOffsetY = 0.1f;

    private readonly List<UnitBase> _targets = new List<UnitBase>();
    private readonly Dictionary<SpriteRenderer, Color> _dimmed = new Dictionary<SpriteRenderer, Color>();
    private readonly Dictionary<Transform, Vector3> _hopped = new Dictionary<Transform, Vector3>();
    private readonly List<GameObject> _rings = new List<GameObject>();
    private RectTransform _pulsed;
    private Vector3 _pulsedScale;

    private RectTransform HudRoot => _hudRoot != null ? _hudRoot : LevelUpUiSpace.RootCanvasOf(_chiefSkillTarget);

    /// <summary>data 카드의 대상을 강조한다 (이전 강조는 먼저 해제).</summary>
    public void Show(LevelUpData data)
    {
        Clear();
        var destination = LevelUpFeedbackTargets.Resolve(data, _gridManager, _chieftainSpawner, _targets);

        if (destination == LevelUpFeedbackDestination.ChiefSkill && _chiefSkillTarget != null)
        {
            _pulsed = _chiefSkillTarget;
            _pulsedScale = _chiefSkillTarget.localScale;
            _chiefSkillTarget.DOKill();
            _ = _chiefSkillTarget.DOScale(_pulsedScale * 1.12f, _hopDuration)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(_chiefSkillTarget.gameObject);
        }
        if (destination != LevelUpFeedbackDestination.Units || _gridManager == null) return;

        var root = HudRoot;
        foreach (var cell in _gridManager.GetOccupiedCells())
        {
            var unit = cell.OccupyingUnit;
            if (unit == null) continue;
            if (_targets.Contains(unit)) Highlight(unit, root);
            else Dim(unit);
        }
    }

    /// <summary>강조를 모두 원래대로 되돌린다.</summary>
    public void Clear()
    {
        foreach (var pair in _dimmed)
            if (pair.Key != null) pair.Key.color = pair.Value;
        _dimmed.Clear();

        foreach (var pair in _hopped)
        {
            if (pair.Key == null) continue;
            pair.Key.DOKill();
            pair.Key.localPosition = pair.Value;
        }
        _hopped.Clear();

        foreach (var ring in _rings)
            if (ring != null) Destroy(ring);
        _rings.Clear();

        if (_pulsed != null)
        {
            _pulsed.DOKill();
            _pulsed.localScale = _pulsedScale;
            _pulsed = null;
        }
        _targets.Clear();
    }

    private void Highlight(UnitBase unit, RectTransform root)
    {
        var tr = unit.transform;
        if (!_hopped.ContainsKey(tr))
        {
            _hopped[tr] = tr.localPosition;
            tr.DOKill();
            _ = tr.DOLocalMoveY(tr.localPosition.y + _hopHeight, _hopDuration)
                .SetEase(Ease.OutQuad).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(unit.gameObject);
        }
        if (root == null) return;
        Image ring = LevelUpFxKit.CreateImage(root, LevelUpFxKit.Ring, _ringSize, _ringColor);
        var ringRt = ring.rectTransform;
        ringRt.position = LevelUpUiSpace.UnitCanvasPoint(root, unit, _ringOffsetY);
        ringRt.localScale = new Vector3(1f, 0.45f, 1f); // 바닥에 깔린 타원
        ringRt.SetAsFirstSibling(); // HUD 버튼보다 뒤, 필드 위
        _ = DOTween.Sequence().SetUpdate(true).SetLink(ring.gameObject).SetLoops(-1, LoopType.Yoyo)
            .Join(ringRt.DOScale(new Vector3(1.15f, 0.52f, 1f), _hopDuration).SetEase(Ease.InOutSine))
            .Join(ring.DOFade(_ringColor.a * 0.5f, _hopDuration));
        _rings.Add(ring.gameObject);
    }

    private void Dim(UnitBase unit)
    {
        foreach (var sr in unit.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == null || _dimmed.ContainsKey(sr)) continue;
            _dimmed[sr] = sr.color;
            sr.color = sr.color * _dimColor;
        }
    }

    private void OnDestroy() => Clear();
}
