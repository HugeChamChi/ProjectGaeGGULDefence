using System.Collections;
using HSD.UI.Setting;
using HSD.UI.Utils;
using UnityEngine;

/// <summary>
/// 설정창 프리팹 미리보기(FxLab_SettingPopup) — 실험실 전용. Play하면 설정창을 열고, 캡처 시 상태별로 찍는다.
/// DI가 없어 오디오·저장은 동작하지 않으므로 표시 상태만 공개 API(SetState)로 바꾼다.
/// </summary>
public sealed class SettingPopupPreview : MonoBehaviour
{
    [SerializeField] private UI_SettingPanel_InGame _panel;
    private bool _captureStarted;

    /// <summary>마지막 캡처 결과(진행 중이면 null).</summary>
    public string CaptureResult { get; private set; } = string.Empty;

    /// <summary>빌더가 미리보기 씬을 만들 때 연결한다.</summary>
    public void Configure(UI_SettingPanel_InGame panel) => _panel = panel;

    private void Start()
    {
        if (_panel != null) _panel.Open();
    }

    /// <summary>기본 / 끔 상태 / 로비 확인 팝업을 게임 뷰 해상도로 저장한다.</summary>
    public void CaptureStates(string folder)
    {
        if (_captureStarted) return; // 플레이당 한 번(CLI 재시도로 두 번 불려도 무시)
        _captureStarted = true;
        CaptureResult = null;
        StartCoroutine(CaptureRoutine(folder));
    }

    private IEnumerator CaptureRoutine(string folder)
    {
        System.IO.Directory.CreateDirectory(folder);
        yield return new WaitForSecondsRealtime(0.6f);
        yield return Shot(folder, "01_default.png");

        // 시안처럼 효과음 끔 + 데미지 플로터 끔
        // 실험실엔 AudioManager가 없어 SetState(슬라이더 콜백 → 오디오 호출)를 쓰지 않고 표시만 바꾼다.
        foreach (var slot in _panel.GetComponentsInChildren<UI_SoundSettingSlot>(true))
        {
            if (slot.Group != AudioGroup.SFX) continue;
            slot.GetComponentInChildren<UnityEngine.UI.Slider>(true).SetValueWithoutNotify(0f);
            slot.GetComponentInChildren<UnityEngine.UI.Toggle>(true).SetIsOnWithoutNotify(false);
            typeof(UI_SoundSettingSlot).GetMethod("UpdateVisual", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(slot, new object[] { false });
        }
        foreach (var toggle in _panel.GetComponentsInChildren<UI_SlidingToggle>(true))
            if (toggle.transform.parent.name.StartsWith("DamageFloater")) toggle.SetState(false, true);
        yield return new WaitForSecondsRealtime(0.4f);
        yield return Shot(folder, "02_design_states.png");

        _panel.ShowConfirm("로비로 이동", "진행중인 게임이 종료되며\n<color=yellow>로비</color>로 돌아갑니다.", null);
        yield return new WaitForSecondsRealtime(0.6f);
        yield return Shot(folder, "03_confirm.png");
        CaptureResult = folder;
    }

    private static IEnumerator Shot(string folder, string file)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, file), tex.EncodeToPNG());
        Destroy(tex);
    }
}
