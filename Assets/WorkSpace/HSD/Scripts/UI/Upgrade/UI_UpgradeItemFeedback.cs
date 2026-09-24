using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HSD.UI.Upgrade
{
    /// <summary>
    /// 강화 카드의 연출 전용 컴포넌트 (게임 상태를 바꾸지 않음). UI_UpgradeItem이 결과에 따라 호출한다.
    /// - 성공: 카드 펀치 + 레벨 숫자 팝/번쩍 + 비용 "-N"이 떠오르며 사라짐
    /// - 실패(재화 부족 등): 카드 좌우 흔들림 + 비용 숫자 빨간 깜빡임
    /// 강화 패널이 게임을 멈춘 상태에서도 재생되도록 모든 트윈은 unscaled 시간으로 돈다.
    /// </summary>
    public class UI_UpgradeItemFeedback : MonoBehaviour
    {
        [Header("Success — card")]
        [SerializeField] private float _cardPunch = 0.08f;
        [SerializeField] private float _cardPunchDuration = 0.25f;
        [Header("Success — level text")]
        [SerializeField] private float _levelPop = 0.6f;
        [SerializeField] private float _levelPopDuration = 0.3f;
        [SerializeField] private Color _levelFlashColor = new Color(1f, 0.85f, 0.3f, 1f);
        [Header("Success — spent cost floater")]
        [SerializeField] private float _costFloatDistance = 70f;
        [Tooltip("비용 숫자 대비 떠오르는 숫자 크기 배율.")]
        [SerializeField] private float _costFloatScale = 1.8f;
        [SerializeField] private float _costFloatDuration = 0.8f;
        [SerializeField] private Color _costFloatColor = new Color(1f, 0.45f, 0.35f, 1f);
        [Header("Rejected")]
        [SerializeField] private float _shakeAngle = 6f;
        [SerializeField] private float _shakeDuration = 0.3f;
        [SerializeField] private Color _rejectColor = new Color(1f, 0.25f, 0.25f, 1f);

        private RectTransform _card;
        private TextMeshProUGUI _level;
        private TextMeshProUGUI _cost;
        private RectTransform _floatOrigin;
        private Color _levelBaseColor;
        private Color _costBaseColor;

        /// <summary>연출 대상 연결. 텍스트는 없어도 된다. floatOrigin: 사용 비용 숫자가 떠오르기 시작할 곳(캐릭터 아이콘). 없으면 비용 위치.</summary>
        public void Bind(RectTransform card, TextMeshProUGUI level, TextMeshProUGUI cost, RectTransform floatOrigin = null)
        {
            _card = card;
            _level = level;
            _cost = cost;
            _floatOrigin = floatOrigin;
            if (_level != null) _levelBaseColor = _level.color;
            if (_cost != null) _costBaseColor = _cost.color;
        }

        /// <summary>강화 성공 연출. spentCost가 0 이하이면 비용 숫자는 띄우지 않는다.</summary>
        public void PlaySuccess(int spentCost)
        {
            if (_card != null)
            {
                _card.DOKill(true);
                _card.localRotation = Quaternion.identity;
                _card.DOPunchScale(Vector3.one * _cardPunch, _cardPunchDuration, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
            }
            if (_level != null)
            {
                var levelRect = _level.rectTransform;
                levelRect.DOKill(true);
                _level.DOKill(true);
                levelRect.DOPunchScale(Vector3.one * _levelPop, _levelPopDuration, 5, 0.5f).SetUpdate(true).SetLink(gameObject);
                _level.color = _levelFlashColor;
                _level.DOColor(_levelBaseColor, _levelPopDuration * 1.5f).SetUpdate(true).SetLink(gameObject);
            }
            if (spentCost > 0) SpawnCostFloater(spentCost);
        }

        /// <summary>강화 실패 연출 (재화 부족 등).</summary>
        public void PlayRejected()
        {
            if (_card != null)
            {
                _card.DOKill(true);
                _card.localRotation = Quaternion.identity;
                // 레이아웃 그룹이 위치를 관리하므로 위치 대신 회전으로 흔든다.
                _card.DOShakeRotation(_shakeDuration, new Vector3(0f, 0f, _shakeAngle), 18, 0f, true, ShakeRandomnessMode.Harmonic)
                    .SetUpdate(true).SetLink(gameObject)
                    .OnComplete(() => { if (_card != null) _card.localRotation = Quaternion.identity; });
            }
            if (_cost != null)
            {
                _cost.DOKill(true);
                _cost.color = _rejectColor;
                _cost.DOColor(_costBaseColor, _shakeDuration * 1.5f).SetUpdate(true).SetLink(gameObject);
            }
        }

        private void SpawnCostFloater(int spentCost)
        {
            if (_cost == null || _card == null) return;
            var floater = Instantiate(_cost, _card, true);
            floater.name = "SpentCostFloater";
            floater.DOKill();
            floater.raycastTarget = false;
            floater.text = "-" + spentCost.ToString("N0");
            floater.color = _costFloatColor;
            floater.enableAutoSizing = false;
            floater.fontSize = _cost.fontSize * _costFloatScale;
            floater.fontStyle = FontStyles.Bold;
            floater.textWrappingMode = TextWrappingModes.NoWrap;
            floater.alignment = TextAlignmentOptions.Center;
            // 카드 안의 레이아웃 그룹이 복제본 위치를 바꾸지 않도록 제외한다.
            var layout = floater.GetComponent<LayoutElement>();
            if (layout == null) layout = floater.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            floater.transform.SetAsLastSibling();

            // 원본 비용 텍스트의 늘어나는 앵커를 그대로 쓰면 크기가 틀어지므로, 카드 폭의 가운데 정렬 박스로 바꾼다.
            var rect = floater.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(_card.rect.width, floater.fontSize * 1.4f);
            // 이름·비용 글자와 겹치지 않게 캐릭터 아이콘 가운데에서 떠오른다.
            rect.position = _floatOrigin != null
                ? _floatOrigin.TransformPoint(_floatOrigin.rect.center) // 피벗 위치와 무관하게 아이콘 정중앙
                : _cost.rectTransform.position;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(floater.gameObject);
            seq.Join(rect.DOAnchorPosY(rect.anchoredPosition.y + _costFloatDistance, _costFloatDuration).SetEase(Ease.OutSine));
            seq.Join(floater.DOFade(0f, _costFloatDuration * 0.5f).SetDelay(_costFloatDuration * 0.5f).SetEase(Ease.InQuad));
            seq.OnComplete(() => { if (floater != null) Destroy(floater.gameObject); });
        }
    }
}
