using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 레벨업 카드 선택 직후 연출 (LevelUpUI와 같은 GameObject에 부착).
///   선택 카드가 살짝 눌렸다 복귀, 나머지 카드는 축소+페이드 퇴장 → 패널 페이드 아웃.
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

    private LevelUpCollectEffect _collect;

    private void Awake() => _collect = GetComponent<LevelUpCollectEffect>();

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
            await panel.DOFade(0f, _panelFadeOutDuration).SetUpdate(true).SetLink(panel.gameObject)
                       .ToUniTask(cancellationToken: token);

        if (collectIcon && selected != null && _collect != null)
            _collect.Play(selected.GetData(), selected.IconSprite);
    }
}
