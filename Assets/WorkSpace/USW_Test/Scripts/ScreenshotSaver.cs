using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PreviewPanel 영역만 잘라서 PNG로 바탕화면에 저장합니다.
/// WaitForEndOfFrame으로 렌더링 완료 후 ReadPixels를 수행합니다.
/// </summary>
public class ScreenshotSaver : MonoBehaviour
{
    [Header("캡처 영역 (PreviewContainer RectTransform)")]
    [SerializeField] private RectTransform _previewPanel;

    [Header("UI")]
    [SerializeField] private Button           _saveButton;
    [SerializeField] private TextMeshProUGUI  _statusLabel;

    [SerializeField] private float _statusDuration = 3f;

    private void Awake()
    {
        _saveButton.onClick.AddListener(() => StartCoroutine(CaptureRoutine()));
    }

    private IEnumerator CaptureRoutine()
    {
        _saveButton.interactable = false;
        yield return new WaitForEndOfFrame();

        // Screen Space Overlay Canvas에서 GetWorldCorners는 스크린 픽셀 좌표를 반환
        Vector3[] corners = new Vector3[4];
        _previewPanel.GetWorldCorners(corners);

        int x = Mathf.RoundToInt(corners[0].x);
        int y = Mathf.RoundToInt(corners[0].y);
        int w = Mathf.RoundToInt(corners[2].x - corners[0].x);
        int h = Mathf.RoundToInt(corners[2].y - corners[0].y);

        if (w <= 0 || h <= 0)
        {
            ShowStatus("캡처 실패: 미리보기 영역 크기가 0입니다.");
            _saveButton.interactable = true;
            yield break;
        }

        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(x, y, w, h), 0, 0);
        tex.Apply();

        string desktop   = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string savePath  = Path.Combine(desktop, $"ArtPreview_{timestamp}.png");

        File.WriteAllBytes(savePath, tex.EncodeToPNG());
        Destroy(tex);

        ShowStatus($"저장 완료: {Path.GetFileName(savePath)}");
        _saveButton.interactable = true;
    }

    private void ShowStatus(string message)
    {
        if (_statusLabel == null) return;
        _statusLabel.text = message;
        StartCoroutine(ClearStatusAfterDelay());
    }

    private IEnumerator ClearStatusAfterDelay()
    {
        yield return new WaitForSeconds(_statusDuration);
        if (_statusLabel != null) _statusLabel.text = string.Empty;
    }
}
