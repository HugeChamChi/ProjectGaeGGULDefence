using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;

public class RcloneSyncTool : EditorWindow
{
    private string rclonePath = "rclone";
    private string localPath = "";
    private string remotePath = "gdrive_ggd:GGD_Imports";

    // 성능 옵션은 팀원들이 건드릴 필요 없도록 최적화된 값으로 고정
    private const int Transfers = 16;
    private const int Checkers = 64;
    private const string ChunkSize = "64M";
    private const string BufferSize = "32M";

    [MenuItem("Tools/Rclone Sync Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<RcloneSyncTool>("Rclone Sync");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }

    private void OnEnable()
    {
        rclonePath = EditorPrefs.GetString("Rclone_ExecutablePath", "rclone");
        localPath = EditorPrefs.GetString("Rclone_LocalPath", Application.dataPath + "/Imports");
        remotePath = EditorPrefs.GetString("Rclone_RemotePath", "gdrive_ggd:GGD_Imports");
    }

    private void OnDisable()
    {
        EditorPrefs.SetString("Rclone_ExecutablePath", rclonePath);
        EditorPrefs.SetString("Rclone_LocalPath", localPath);
        EditorPrefs.SetString("Rclone_RemotePath", remotePath);
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("🛠️ Rclone 설정", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        rclonePath = EditorGUILayout.TextField("Rclone 경로", rclonePath);
        if (GUILayout.Button("찾기", GUILayout.Width(60)))
        {
            string selected = EditorUtility.OpenFilePanel("rclone.exe 선택", "", "exe");
            if (!string.IsNullOrEmpty(selected)) rclonePath = selected;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        localPath = EditorGUILayout.TextField("로컬 경로", localPath);
        if (GUILayout.Button("찾기", GUILayout.Width(60)))
        {
            string selected = EditorUtility.OpenFolderPanel("로컬 폴더 선택", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selected)) localPath = selected;
        }
        EditorGUILayout.EndHorizontal();

        remotePath = EditorGUILayout.TextField("구글 드라이브 경로", remotePath);

        GUILayout.Space(20);
        EditorGUILayout.HelpBox("성능 최적화(Size Only, Fast List 등)가 자동으로 적용되어 있습니다.", MessageType.Info);

        GUILayout.Space(20);

        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("🚀 구글 드라이브로 업로드 (Upload)", GUILayout.Height(45))) Upload();

        GUI.backgroundColor = new Color(0.7f, 0.8f, 1f);
        GUILayout.Space(10);
        if (GUILayout.Button("📥 구글 드라이브에서 다운로드 (Download)", GUILayout.Height(45))) Download();
        
        GUI.backgroundColor = Color.white;
    }

    private void Upload()
    {
        if (!ValidateSettings()) return;
        if (EditorUtility.DisplayDialog("업로드", "로컬의 변경사항을 구글 드라이브에 반영하시겠습니까?", "실행", "취소")) 
            RunRclone("sync", localPath, remotePath);
    }

    private void Download()
    {
        if (!ValidateSettings()) return;
        if (EditorUtility.DisplayDialog("다운로드", "구글 드라이브의 최신 데이터를 로컬로 받아오시겠습니까?", "실행", "취소")) 
            RunRclone("sync", remotePath, localPath);
    }

    private bool ValidateSettings()
    {
        if (string.IsNullOrEmpty(localPath) || !Directory.Exists(localPath))
        {
            EditorUtility.DisplayDialog("경고", "로컬 경로를 확인해주세요.", "확인");
            return false;
        }
        return true;
    }

    private void RunRclone(string command, string src, string dest)
    {
        string rcloneExec = rclonePath;
        if (!rcloneExec.Contains("\"") && rcloneExec.Contains(" ")) rcloneExec = $"\"{rcloneExec}\"";

        // 팀원용은 복잡한 옵션 없이 최적화 값 고정 적용
        string rcloneArgs = $"{command} \"{src}\" \"{dest}\" " +
                            $"--transfers {Transfers} " +
                            $"--checkers {Checkers} " +
                            $"--drive-chunk-size {ChunkSize} " +
                            $"--buffer-size {BufferSize} " +
                            $"--fast-list --size-only --progress";

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/k \"{rcloneExec} {rcloneArgs}\"",
            UseShellExecute = true, 
            CreateNoWindow = false
        };

        try { Process.Start(startInfo); }
        catch (System.Exception e) { UnityEngine.Debug.LogError(e.Message); }
    }
}
