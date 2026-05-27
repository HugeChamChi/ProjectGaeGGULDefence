using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DroneManager))]
public class DroneManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("테스트", EditorStyles.boldLabel);

        GUI.enabled = Application.isPlaying;

        if (GUILayout.Button("집결 발동", GUILayout.Height(36)))
        {
            var manager = (DroneManager)target;
            manager.TriggerTestRally();
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("플레이 모드에서만 사용 가능합니다.", MessageType.Info);

        GUI.enabled = true;
    }
}
