using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 레벨업 카드 선택 직후 연출 (LevelUpUI와 같은 GameObject에 부착).
///   선택 카드가 살짝 눌렸다 복귀(+ 선택 강조 이펙트 슬롯), 나머지 카드는 축소+페이드 퇴장 → 패널 페이드 아웃.
///   이어서 같은 오브젝트의 LevelUpCollectEffect가 있으면 획득 연출을 시작한다 (게임 재개와 동시에 진행).
/// </summary>
public class LevelUpSelectSequence : MonoBehaviour
{
    [Tooltip("선택되지 않은 카드가 사라지는 시간.")]
    [SerializeField, Min(0.01f)] private float _othersExitDuration = 0.2f;
    [SerializeField, Min(0f)] private float _othersExitScale = 0.85f;
    [Tooltip("선택 카드가 눌리는 배율.")]
    [SerializeField, Min(0f)] private float _pressScale = 0.92f;
    [Tooltip("눌림 전체 시간 (눌림 + 복귀). 이후 패널이 닫힌다.")]
    [SerializeField, Min(0.01f)] private float _pressDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float _panelFadeOutDuration = 0.15f;

    [Header("이펙트 슬롯 (비우면 없음)")]
    [Tooltip("선택 카드 강조 이펙트 (레벨업 요구사항 ④). CardRevealFx가 있으면 선택 카드의 등급·모양으로 재생한다.")]
    [SerializeField] private GameObject _cardSelectPrefab;
    [Tooltip("선택 이펙트 인스턴스를 자동 제거할 시간(초, unscaled).")]
    [SerializeField, Min(0.1f)] private float _effectLifetime = 1f;

    private LevelUpCollectEffect _collect;
    private LevelUpFxKit _fx;

    private LevelUpFxKit Fx => _fx ??= new LevelUpFxKit(_effectLifetime, Color.white);

    private void Awake() => _collect = GetComponent<LevelUpCollectEffect>();

    private void OnDestroy() => _fx?.DestroyAll();

    /// <summary>
    /// 선택 연출을 재생하고 패널이 닫히면 반환한다. collectIcon이면 획득 연출을 백그라운드로 시작한다.
    /// panel이 null이면(리롤) 패널을 닫지 않는다.
    /// </summary>
    public async UniTask PlayAsync(LevelUpCardUI selected, IReadOnlyList<LevelUpCardUI> cards, CanvasGroup panel,
                                   bool collectIcon, CancellationToken token)
    {
        if (selected != null)
        {
            // LevelUpCardUI.Select()의 확대 트윈 대신 살짝 눌렸다 복귀한다.
            selected.transform.DOKill();
            selected.transform.localScale = Vector3.one;
            float half = _pressDuration * 0.4f;
            _ = DOTween.Sequence().SetUpdate(true).SetLink(selected.gameObject)
                .Append(selected.transform.DOScale(_pressScale, half).SetEase(Ease.OutQuad))
                .Append(selected.transform.DOScale(1f, _pressDuration - half).SetEase(Ease.OutBack));
            PlaySelectFx(selected);
        }

        foreach (var card in cards)
        {
            if (card == null || card == selected) continue;
            var group = card.GetComponent<CanvasGroup>();
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(card.gameObject)
                .Join(card.transform.DOScale(_othersExitScale, _othersExitDuration).SetEase(Ease.InBack));
            if (group != null) _ = seq.Join(group.DOFade(0f, _othersExitDuration));
        }

        await UniTask.Delay(LevelUpUiSpace.Ms(_pressDuration), DelayType.Realtime, cancellationToken: token);
        if (panel != null)
        {
            // 취소 시 대기만 끝내고(CancelAwait) 트윈은 여기서 종료한다 — Kill 콜백 안에서 이어지는 코드가
            // 다른 트윈을 Kill하면 DOTween 내부 목록이 꼬인다 (LevelUpRevealSequence와 같은 방식).
            var fade = panel.DOFade(0f, _panelFadeOutDuration).SetUpdate(true).SetLink(panel.gameObject);
            try { await fade.ToUniTask(TweenCancelBehaviour.CancelAwait, token); }
            finally { if (fade.IsActive()) fade.Kill(); }
        }

        if (collectIcon && selected != null && _collect != null)
            _collect.Play(selected.GetData(), selected.IconSprite);
    }

    // 선택 카드 위(카드 컨테이너의 부모 = 레벨업 패널 안)에 강조 이펙트를 생성한다 — 패널이 닫힐 때 함께 사라진다.
    private void PlaySelectFx(LevelUpCardUI selected)
    {
        if (_cardSelectPrefab == null) return;
        var parent = selected.transform.parent;
        var root = parent != null ? (parent.parent as RectTransform ?? parent as RectTransform) : null;
        var fx = Fx.Spawn(_cardSelectPrefab, root, selected.transform.position, 0f);
        if (fx != null && fx.TryGetComponent<CardRevealFx>(out var cardFx)) cardFx.PlayOnCard(selected);
    }
}
