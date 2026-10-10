using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 중앙 알림 실험실(FxLab_CenterToast) 드라이버 (사용자 요청 2026-10-02, 참고 design/중앙팝업1.gif).
/// 재사용 컴포넌트 CenterToast를 그대로 띄워 보고, 연출 방식(A 스택 / B 라인 스플릿 / C 글래스 슬라이드 — 모던, D 펀치 글자, E·F·G·H 조합)을 비교한다.
/// 겹쳐 올라가기 실험실(FxLab_CenterToastRise)도 같은 드라이버를 쓴다 — 버튼에 F·G·H만 연결.
/// 버튼: 문구 한 번씩, 같은 문구 연타(5번), 서로 다른 문구 섞어 연타, 속도, 자동 데모. 문구는 실험용 예시.
/// 실험실 전용 — FxLabCapture 캡처 대상 (Play() = 자동 데모 처음부터, 1배속).
/// </summary>
public sealed class CenterToastLab : MonoBehaviour, IFxLabPlayable
{
    private const string FoodMessage = "식량이 부족합니다.";
    private const string SeatMessage = "배치할 자리가 없습니다.";
    private const string MergeMessage = "합성할 유닛이 없습니다.";
    private const string LongMessage = "보스를 처치하면 다음 라운드가 열립니다.";
    private const string InfoMessage = "토템을 배치했습니다.";
    private const float MaxStep = 1f / 20f;
    private static readonly Color ButtonIdle = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color ButtonSelected = new Color(0.35f, 0.8f, 0.45f, 0.85f);
    private static readonly string[] StyleNames = { "A 스택(모던)", "B 라인 스플릿", "C 글래스 슬라이드", "D 펀치 글자", "E = B 디자인 + A 연출(쌓기)", "F = B 디자인 + C 연출(슬라이드)", "G 슬라이드 + 글자 겹쳐 올라감", "H 슬라이드 + 카드째 겹쳐 올라감", "I 글자 겹침 + 화면 전체 띠" };

    [SerializeField] private CenterToast _toast;
    [SerializeField] private CenterToastSettings _settings;
    [Tooltip("연출 방식 버튼")]
    [SerializeField] private Image[] _styleButtons;
    [Tooltip("버튼 순서대로 띄울 방식 (CenterToastStyle 정수). 비우면 버튼 i = 방식 i")]
    [SerializeField] private int[] _styleOrder;
    [SerializeField] private TextMeshProUGUI _status;
    [SerializeField] private float[] _speeds = { 1f, 0.5f, 0.25f };
    [Tooltip("연타 시뮬레이션 간격 (초) — 손가락으로 빠르게 두드리는 정도")]
    [SerializeField] private float _tapInterval = 0.09f;
    [SerializeField] private int _tapCount = 5;
    [Tooltip("자동 데모 단계 사이 간격 (초)")]
    [SerializeField] private float _autoInterval = 1.8f;
    [SerializeField] private bool _auto = true;

    private readonly List<(float At, string Message, CenterToastKind Kind)> _queue = new List<(float, string, CenterToastKind)>();
    private float _clock;
    private float _nextAuto;
    private int _autoStep;
    private int _speedIndex;
    private bool _useUnscaledTime = true;

    /// <inheritdoc />
    public bool UseUnscaledTime
    {
        get => _useUnscaledTime;
        set { _useUnscaledTime = value; if (_toast != null) _toast.UseUnscaledTime = value; }
    }

    private float Speed => _speeds.Length > 0 ? _speeds[Mathf.Clamp(_speedIndex, 0, _speeds.Length - 1)] : 1f;

    private void Start()
    {
        if (_toast == null || _settings == null)
        {
            Debug.LogError("[CenterToastLab] CenterToast/설정 미연결", this);
            enabled = false;
            return;
        }
        Refresh();
    }

    private void Update()
    {
        float dt = Mathf.Min(_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, MaxStep) * Speed;
        _clock += dt;
        _toast.TimeScale = Speed;
        for (int i = 0; i < _queue.Count; i++)
        {
            if (_queue[i].At > _clock) continue;
            _toast.Show(_queue[i].Message, _queue[i].Kind);
            _queue.RemoveAt(i--);
        }
        if (_auto && _clock >= _nextAuto) RunAutoStep();
        if (Time.frameCount % 10 == 0) UpdateStatus();
    }

    // ── 버튼 ──────────────────────────────────────────────────

    /// <summary>캡처용: 자동 데모를 1배속으로 처음부터.</summary>
    public void Play()
    {
        _speedIndex = 0;
        _queue.Clear();
        _toast.Clear();
        _auto = true;
        _autoStep = 0;
        _nextAuto = _clock;
    }

    /// <summary>연출 방식 선택 (0=A 스택, 1=B 라인 스플릿, 2=C 글래스 슬라이드, 3=D 펀치 글자, 4=E B+A, 5=F B+C, 6=G 글자 겹쳐 올라감, 7=H 카드째). 설정 에셋에도 저장된다.</summary>
    public void SelectStyle(int index)
    {
        _settings.Style = (CenterToastStyle)Mathf.Clamp(index, 0, StyleNames.Length - 1);
        _toast.Style = _settings.Style;
        _queue.Clear();
        _autoStep = 0;
        _nextAuto = _clock + 0.2f;
        Refresh();
    }

    public void ShowFood() => Enqueue(FoodMessage, CenterToastKind.Warning, 0f);
    public void ShowSeat() => Enqueue(SeatMessage, CenterToastKind.Warning, 0f);
    public void ShowMergeFail() => Enqueue(MergeMessage, CenterToastKind.Warning, 0f);
    public void ShowLong() => Enqueue(LongMessage, CenterToastKind.Info, 0f);
    public void ShowInfo() => Enqueue(InfoMessage, CenterToastKind.Info, 0f);

    /// <summary>같은 문구 연타 (식량 부족 버튼을 빠르게 여러 번).</summary>
    public void TapSpam()
    {
        for (int i = 0; i < _tapCount; i++) Enqueue(FoodMessage, CenterToastKind.Warning, i * _tapInterval);
    }

    /// <summary>서로 다른 문구를 섞어 연타.</summary>
    public void MixedSpam()
    {
        Enqueue(FoodMessage, CenterToastKind.Warning, 0f);
        Enqueue(SeatMessage, CenterToastKind.Warning, _tapInterval * 1.5f);
        Enqueue(FoodMessage, CenterToastKind.Warning, _tapInterval * 3f);
        Enqueue(MergeMessage, CenterToastKind.Warning, _tapInterval * 4.5f);
    }

    /// <summary>1배 → 0.5배 → 0.25배.</summary>
    public void NextSpeed()
    {
        _speedIndex = (_speedIndex + 1) % Mathf.Max(1, _speeds.Length);
        UpdateStatus();
    }

    /// <summary>자동 데모 켜기/끄기.</summary>
    public void ToggleAuto()
    {
        _auto = !_auto;
        _nextAuto = _clock;
        UpdateStatus();
    }

    // ── 내부 ──────────────────────────────────────────────────

    private void Enqueue(string message, CenterToastKind kind, float delay) => _queue.Add((_clock + delay, message, kind));

    // 데모 순서: 한 번 → 같은 문구 연타 → 다른 문구 → 섞어 연타 → 안내 → 반복
    private void RunAutoStep()
    {
        switch (_autoStep % 5)
        {
            case 0: ShowFood(); break;
            case 1: TapSpam(); break;
            case 2: ShowSeat(); break;
            case 3: MixedSpam(); break;
            default: ShowInfo(); break;
        }
        _autoStep++;
        _nextAuto = _clock + _autoInterval;
    }

    private void Refresh()
    {
        int current = (int)_settings.Style;
        if (_styleButtons != null)
            for (int i = 0; i < _styleButtons.Length; i++)
                if (_styleButtons[i] != null) _styleButtons[i].color = StyleOf(i) == current ? ButtonSelected : ButtonIdle;
        UpdateStatus();
    }

    private int StyleOf(int button) => _styleOrder != null && button < _styleOrder.Length ? _styleOrder[button] : button;

    private void UpdateStatus()
    {
        if (_status == null || _settings == null) return;
        int current = Mathf.Clamp((int)_settings.Style, 0, StyleNames.Length - 1);
        _status.text = $"{StyleNames[current]}  /  {Speed:0.##}배속  /  자동 데모 {(_auto ? "켬" : "끔")}\n"
            + "사용법: _centerToast.Show(\"식량이 부족합니다.\", CenterToastKind.Warning)";
    }
}
