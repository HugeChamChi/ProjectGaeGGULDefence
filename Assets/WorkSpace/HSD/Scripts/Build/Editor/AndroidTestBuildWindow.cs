using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GaeGGUL.Build.Editor
{
    public sealed class AndroidTestBuildWindow : EditorWindow
    {
        private string _password = Environment.GetEnvironmentVariable("GAEGGUL_KEYSTORE_PASS")
            ?? Environment.GetEnvironmentVariable("GAEGGUL_KEYSTORE_PASS", EnvironmentVariableTarget.User)
            ?? "";
        private int _versionCode;

        [MenuItem("GaeGGUL/Build/Android Test Build Tool")]
        public static void Open() => GetWindow<AndroidTestBuildWindow>("Android Test Build");

        private void OnEnable() => _versionCode = PlayerSettings.Android.bundleVersionCode;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("서명된 Android 테스트 빌드", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("상점 테스트 전용 보라색 타이틀 → 상점 테스트 로비 · DEV 차트 · 디버깅 끔\nAAB: Play 내부 테스트 업로드용 / APK: 기기 직접 설치용", MessageType.Info);
            EditorGUILayout.LabelField("패키지", PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android));
            EditorGUILayout.LabelField("버전", PlayerSettings.bundleVersion);
            EditorGUILayout.LabelField("키스토어", PlayerSettings.Android.keystoreName);
            EditorGUILayout.LabelField("키 별칭", PlayerSettings.Android.keyaliasName);
            _password = EditorGUILayout.PasswordField("키스토어 / 키 비밀번호", _password);
            _versionCode = EditorGUILayout.IntField("Version Code", _versionCode);
            EditorGUILayout.HelpBox("Play에 새 버전을 올릴 때는 기존 업로드보다 큰 Version Code를 입력하세요.\n서명은 기존 Publishing Settings의 키스토어와 별칭을 사용합니다.", MessageType.None);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer ||
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android || _versionCode < 1 || string.IsNullOrEmpty(_password)))
            {
                if (GUILayout.Button("서명 AAB 빌드")) Queue(true);
                if (GUILayout.Button("서명 APK 빌드")) Queue(false);
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorGUILayout.HelpBox("Build Profiles에서 Android로 Switch Platform 후 사용하세요.", MessageType.Warning);
            if (GUILayout.Button("출력 폴더 열기"))
            {
                Directory.CreateDirectory("Builds/InternalTest");
                EditorUtility.RevealInFinder(Path.GetFullPath("Builds/InternalTest"));
            }
        }

        private void Queue(bool appBundle)
        {
            try
            {
                Debug.Log(AndroidBuildPipeline.QueueInternalBuild(appBundle, _password));
                PlayerSettings.Android.bundleVersionCode = _versionCode;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorUtility.DisplayDialog("빌드 시작 실패", error.Message, "확인");
            }
        }
    }
}
