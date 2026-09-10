using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 HP 바가 일정 줄 수(기본 10줄)만큼 떨어질 때마다 유리깨짐 플립북(스프라이트 시퀀스)을 1회 재생.
/// UI_BossHpBar.OnCurrentLineChanged 를 구독한다. 무채색 프레임이라 _tint 로 색을 입힐 수 있다.
/// </summary>
public class BossHpBreakFlipbook : MonoBehaviour
{
    [Header("References")]
    [Tooltip("줄 변경을 감지할 HP 바")]
    [SerializeField] private UI_BossHpBar _hpBar;

    [Tooltip("프레임을 표시할 UI Image (평소엔 비활성)")]
    [SerializeField] private Image _target;

    [Tooltip("슬라이스한 유리깨짐 스프라이트 14장 (00~13 순서)")]
    [SerializeField] private Sprite[] _frames;

    [Header("Trigger")]
    [Min(1)]
    [Tooltip("이 줄 수만큼 떨어질 때마다 재생 (예: 10)")]
    [SerializeField] private int _lineInterval = 10;

    [Header("Playback")]
    [Tooltip("전체 재생 시간(초). 프레임당 시간 = duration / 프레임 수")]
    [SerializeField] private float _duration = 0.45f;

    [Tooltip("무채색 프레임에 입힐 색 (흰색 = 원본 명암 유지)")]
    [SerializeField] private Color _tint = Color.white;

    private int _lastBucket = int.MinValue;
    private int _frameIndex;
    private float _timer;
    private bool _playing;

    private void Awake()
    {
        if (_target != null)
            _target.enabled = false;
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

        Stop();
    }

    private void HandleLineChanged(int currentLine)
    {
        int bucket = currentLine / Mathf.Max(1, _lineInterval);

        if (_lastBucket == int.MinValue)
        {
            _lastBucket = bucket;
            return;
        }

        if (bucket < _lastBucket)
            Play();

        _lastBucket = bucket;
    }

    /// <summary>플립북을 처음부터 1회 재생 (외부에서 수동 호출도 가능).</summary>
    public void Play()
    {
        if (_target == null)
        {
            Debug.LogWarning("[BossHpBreakFlipbook] Target Image 가 연결되지 않아 재생 불가.", this);
            return;
        }
        if (_frames == null || _frames.Length == 0)
        {
            Debug.LogWarning("[BossHpBreakFlipbook] Frames 가 비어있어 재생 불가. 슬라이스한 스프라이트를 넣어주세요.", this);
            return;
        }

        _frameIndex = 0;
        _timer = 0f;
        _target.sprite = _frames[0];
        _target.color = _tint;
        _target.enabled = true;
        _playing = true;
    }

    private void Stop()
    {
        _playing = false;
        if (_target != null)
            _target.enabled = false;
    }

#if UNITY_EDITOR
    // 에디터에서 표시용 Image 가 흰 박스로 보이지 않게 꺼둔다 (플레이 시 Play()가 켬).
    private void OnValidate()
    {
        if (!Application.isPlaying && _target != null)
            _target.enabled = false;
    }
#endif

    private void Update()
    {
        if (!_playing)
            return;

        // 타임스케일 영향 안 받게 unscaled 사용 (일시정지 중에도 연출)
        _timer += Time.unscaledDeltaTime;
        float frameTime = _duration / Mathf.Max(1, _frames.Length);

        while (_timer >= frameTime)
        {
            _timer -= frameTime;
            _frameIndex++;

            if (_frameIndex >= _frames.Length)
            {
                Stop();
                return;
            }

            _target.sprite = _frames[_frameIndex];
        }
    }
}
