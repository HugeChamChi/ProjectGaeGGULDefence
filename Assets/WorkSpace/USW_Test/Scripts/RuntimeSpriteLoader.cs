using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 외부 PNG 파일을 런타임에 로드합니다.
/// Windows 네이티브 파일 대화상자(Comdlg32.dll)를 STA 스레드에서 실행해
/// Unity 메인 루프를 블로킹하지 않습니다.
/// </summary>
public class RuntimeSpriteLoader : MonoBehaviour
{
    /// <summary>텍스처 로드 완료 시 발생. (texture, fileName)</summary>
    public event Action<Texture2D, string> OnTextureLoaded;

    [SerializeField] private Button _loadButton;

    private volatile string _pendingPath;
    private Texture2D _currentTexture;

    private void Awake()
    {
        _loadButton.onClick.AddListener(OpenFileDialogOnThread);
    }

    // STA 스레드에서 받은 경로를 메인 스레드 Update에서 처리
    private void Update()
    {
        if (_pendingPath == null) return;
        string path = _pendingPath;
        _pendingPath = null;
        LoadTexture(path);
    }

    private void OpenFileDialogOnThread()
    {
        var thread = new Thread(ShowNativeFileDialog);
        thread.SetApartmentState(ApartmentState.STA); // Windows COM 대화상자 필수
        thread.Start();
    }

    private void ShowNativeFileDialog()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        var ofn = new OpenFileName();
        ofn.structSize    = Marshal.SizeOf(typeof(OpenFileName));
        ofn.filter        = "PNG 파일\0*.png\0모든 파일\0*.*\0";
        ofn.file          = new string(new char[260]); // MAX_PATH
        ofn.maxFile       = 260;
        ofn.fileTitle     = new string(new char[64]);
        ofn.maxFileTitle  = 64;
        ofn.initialDir    = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        ofn.title         = "PNG 파일 선택";
        // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY
        ofn.flags         = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000004;

        if (GetOpenFileName(ofn))
            _pendingPath = ofn.file;
#else
        Debug.LogWarning("[Loader] 파일 대화상자는 Windows 빌드에서만 지원됩니다.");
#endif
    }

    /// <summary>Phase 2 DragAndDropHandler에서도 직접 호출 가능합니다.</summary>
    public void LoadTextureFromPath(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[Loader] 파일 없음: {path}");
            return;
        }
        LoadTexture(path);
    }

    private void LoadTexture(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        if (_currentTexture != null) Destroy(_currentTexture);

        _currentTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!_currentTexture.LoadImage(bytes))
        {
            Debug.LogWarning($"[Loader] 이미지 로드 실패: {path}");
            Destroy(_currentTexture);
            _currentTexture = null;
            return;
        }

        OnTextureLoaded?.Invoke(_currentTexture, Path.GetFileName(path));
    }

    private void OnDestroy()
    {
        if (_currentTexture != null) Destroy(_currentTexture);
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // Windows OPENFILENAME 구조체. CharSet.Auto → Windows에서 Unicode 자동 적용.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class OpenFileName
    {
        public int    structSize;
        public IntPtr dlgOwner;
        public IntPtr instance;
        public string filter;
        public string customFilter;
        public int    maxCustFilter;
        public int    filterIndex;
        public string file;         // 미리 new string(new char[260])으로 버퍼 할당 필요
        public int    maxFile;
        public string fileTitle;
        public int    maxFileTitle;
        public string initialDir;
        public string title;
        public int    flags;
        public short  fileOffset;
        public short  fileExtension;
        public string defExt;
        public IntPtr custData;
        public IntPtr hook;
        public string templateName;
        public IntPtr reservedPtr;
        public int    reservedInt;
        public int    flagsEx;
    }

    [DllImport("Comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
#endif
}
