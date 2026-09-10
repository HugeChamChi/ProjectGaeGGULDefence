using UnityEngine;
using DG.Tweening;

/// <summary>
/// 보스 HP 바가 일정 줄 수(기본 10줄)만큼 떨어질 때마다 DOTween 으로 흔들림 연출.
/// UI_BossHpBar.OnCurrentLineChanged 를 구독한다.
/// </summary>
public class BossHpBarShake : MonoBehaviour
{
    [Header("References")]
    [Tooltip("줄 변경을 감지할 HP 바")]
    [SerializeField] private UI_BossHpBar _hpBar;

    [Tooltip("흔들 대상 RectTransform (비우면 이 오브젝트)")]
    [SerializeField] private RectTransform _shakeTarget;

    [Header("Trigger")]
    [Min(1)]
    [Tooltip("이 줄 수만큼 떨어질 때마다 흔듦 (예: 10)")]
    [SerializeField] private int _lineInterval = 10;

    [Header("Shake (DOTween)")]
    [SerializeField] private float _duration = 0.4f;
    [SerializeField] private float _strength = 24f;
    [SerializeField] private int _vibrato = 18;
    [SerializeField] private float _randomness = 90f;

    private int _lastBucket = int.MinValue;
    private Vector2 _basePos;
    private Tween _shakeTween;

    private void Reset() => _shakeTarget = GetComponent<RectTransform>();

    private void Awake()
    {
        if (_shakeTarget == null)
            _shakeTarget = GetComponent<RectTransform>();

        if (_shakeTarget != null)
            _basePos = _shakeTarget.anchoredPosition;
    }

    private void OnEnable()
    {
        if (_hpBar != null)
            _hpBar.OnCurrentLineChanged += HandleLineChanged;
    }

    private void OnDisable()
    {
        if (_hpBar != null)
            _hpBar.OnCurrentLineChanged -= HandleLineChanged;

        KillShake();
    }

    private void HandleLineChanged(int currentLine)
    {
        int bucket = currentLine / Mathf.Max(1, _lineInterval);

        // 최초 1회는 기준만 잡고 흔들지 않음
        if (_lastBucket == int.MinValue)
        {
            _lastBucket = bucket;
            return;
        }

        // 버킷이 내려갔을 때(= _lineInterval 줄 이상 떨어짐)만 흔듦
        if (bucket < _lastBucket)
            Shake();

        _lastBucket = bucket;
    }

    /// <summary>즉시 흔들림 연출 실행 (외부에서 수동 호출도 가능).</summary>
    public void Shake()
    {
        if (_shakeTarget == null)
            return;

        KillShake();

        _shakeTarget.anchoredPosition = _basePos;
        _shakeTween = _shakeTarget
            .DOShakeAnchorPos(_duration, _strength, _vibrato, _randomness, false, true)
            .SetUpdate(true) // 타임스케일 영향 안 받게 (일시정지 중에도 연출)
            .OnComplete(() => _shakeTarget.anchoredPosition = _basePos);
    }

    private void KillShake()
    {
        if (_shakeTween != null && _shakeTween.IsActive())
        {
            _shakeTween.Kill();
            _shakeTarget.anchoredPosition = _basePos;
        }

        _shakeTween = null;
    }

    private void OnDestroy() => KillShake();
}
