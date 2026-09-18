using UnityEditor;
using UnityEngine;

/// <summary>경험치 경로의 공간 제어점과 안전한 편집 모드 미리보기를 제공한다.</summary>
[CustomEditor(typeof(ExpEffectController))]
public sealed class ExpEffectControllerEditor : Editor
{
    private bool _editing = true;
    private bool _preview;
    private float _progress;
    private double _lastTime;
    private readonly Vector3[] _samples = new Vector3[97];
    private readonly float[] _distances = new float[97];

    private void OnEnable()
    {
        _lastTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private void OnDisable() => EditorApplication.update -= Tick;

    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_preview && target != null)
        {
            _progress = Mathf.Repeat(_progress + (float)(now - _lastTime) / ((ExpEffectController)target).TravelDuration, 1f);
            SceneView.RepaintAll();
            Repaint();
        }
        _lastTime = now;
    }

    /// <summary>공간 편집과 재생 버튼을 표시한다.</summary>
    public override void OnInspectorGUI()
    {
        ExpEffectController controller = (ExpEffectController)target;
        serializedObject.Update();
        SerializedProperty particle = serializedObject.FindProperty("particleImage");
        particle.objectReferenceValue = EditorGUILayout.ObjectField("Particle Prefab", particle.objectReferenceValue,
            typeof(AssetKits.ParticleImage.ParticleImage), false);
        if (particle.objectReferenceValue != null && !EditorUtility.IsPersistent(particle.objectReferenceValue))
            EditorGUILayout.HelpBox("씬 오브젝트가 원본으로 연결되어 있습니다. 프로젝트의 ExpEffect 프리팹을 연결하세요.", MessageType.Error);
        DrawPropertiesExcluding(serializedObject, "m_Script", "particleImage", "_leftRoute", "_rightRoute", "_mirrorRight");
        SerializedProperty mirror = serializedObject.FindProperty("_mirrorRight");
        bool previous = mirror.boolValue;
        EditorGUILayout.PropertyField(mirror, new GUIContent("좌우 대칭"));
        serializedObject.ApplyModifiedProperties();
        if (previous && !controller.MirrorRight)
        {
            Undo.RecordObject(controller, "Separate EXP right route");
            controller.RightRoute.CopyMirroredFrom(controller.LeftRoute);
            Save(controller);
        }
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Scene 뷰: 큰 점 = 경유 위치, 작은 점 = 곡선 손잡이.\n양 끝은 Spawn Point / Attractor Target을 따릅니다.\n청록색은 왼쪽, 노란색은 오른쪽입니다. 화면 밖 점도 이동할 수 있습니다.", MessageType.Info);
        _editing = EditorGUILayout.Toggle("씬에서 경로 편집", _editing);
        if (!controller.TryGetEndpoints(out _, out _))
            EditorGUILayout.HelpBox("Route Canvas, Spawn Point, Attractor Target을 연결하세요. 기본 연결 버튼은 같은 씬의 ExpBarUI를 찾습니다.", MessageType.Warning);
        if (GUILayout.Button("Canvas / EXP 도착점 기본 연결"))
            ExpEffectRouteSetup.Configure(controller);
        if (GUILayout.Button("Scene 뷰에서 전체 경로 보기")) Frame(controller);
        _preview = EditorGUILayout.Toggle("경로 미리보기 (경험치 지급 없음)", _preview);
        EditorGUI.BeginChangeCheck();
        _progress = EditorGUILayout.Slider("미리보기 위치", _progress, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("파티클 미리보기 (Play Mode / 경험치 없음)")) controller.PreviewEffect();
            if (GUILayout.Button("경험치 지급 테스트 (Play Mode)")) controller.TestFireEffect();
        }
    }

    private void OnSceneGUI()
    {
        ExpEffectController controller = (ExpEffectController)target;
        if (!controller.TryGetEndpoints(out Vector2 start, out Vector2 end)) return;
        using (new Handles.DrawingScope(Color.white))
        {
            Vector3[] corners = new Vector3[4];
            controller.RouteCanvas.GetWorldCorners(corners);
            Handles.DrawDottedLines(new[] {corners[0], corners[1], corners[1], corners[2], corners[2], corners[3], corners[3], corners[0]}, 5f);
            DrawRoute(controller, controller.LeftRoute, false, start, end, Color.cyan);
            DrawRoute(controller, controller.MirrorRight ? controller.LeftRoute : controller.RightRoute,
                controller.MirrorRight, start, end, Color.yellow);
            Handles.Label(controller.NormalizedToWorld(start), "  BOSS / START");
            Handles.Label(controller.NormalizedToWorld(end), "  EXP / ARRIVAL");
        }
    }

    private void DrawRoute(ExpEffectController controller, ExpEffectRoute route, bool mirror, Vector2 start, Vector2 end, Color color)
    {
        if (!route.IsValid) return;
        Handles.color = color;
        for (int i = 0; i < 9; i += 3)
        {
            Vector3 a = controller.NormalizedToWorld(route.GetPoint(i, start, end, mirror));
            Vector3 b = controller.NormalizedToWorld(route.GetPoint(i + 1, start, end, mirror));
            Vector3 c = controller.NormalizedToWorld(route.GetPoint(i + 2, start, end, mirror));
            Vector3 d = controller.NormalizedToWorld(route.GetPoint(i + 3, start, end, mirror));
            Handles.DrawBezier(a, d, b, c, color, null, 3f);
            if (_editing) { Handles.DrawDottedLine(a, b, 4f); Handles.DrawDottedLine(c, d, 4f); }
        }
        if (_editing && !Application.isPlaying)
        {
            for (int i = 1; i < ExpEffectRoute.PointCount - 1; i++)
            {
                Vector3 world = controller.NormalizedToWorld(route.GetPoint(i, start, end, mirror));
                float size = HandleUtility.GetHandleSize(world) * (i % 3 == 0 ? 0.085f : 0.045f);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Move EXP route point");
                    route.SetPoint(i, controller.WorldToNormalized(moved), start, end, mirror);
                    Save(controller);
                }
            }
        }
        // 실제 이동과 동일한 거리 기반 미리보기. 그래프의 매개변수 속도 차이를 제거한다.
        _distances[0] = 0f;
        for (int i = 0; i < _samples.Length; i++)
        {
            _samples[i] = controller.NormalizedToWorld(route.Evaluate(i / 96f, start, end, mirror));
            if (i > 0) _distances[i] = _distances[i - 1] + Vector3.Distance(_samples[i - 1], _samples[i]);
        }
        float distance = _progress * _distances[96];
        int sample = 1;
        while (sample < 96 && _distances[sample] < distance) sample++;
        Vector3 position = Vector3.Lerp(_samples[sample - 1], _samples[sample], Mathf.InverseLerp(_distances[sample - 1], _distances[sample], distance));
        Handles.SphereHandleCap(0, position, Quaternion.identity, HandleUtility.GetHandleSize(position) * 0.12f, EventType.Repaint);
    }

    private static void Save(ExpEffectController controller)
    {
        EditorUtility.SetDirty(controller);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        SceneView.RepaintAll();
    }

    private static void Frame(ExpEffectController controller)
    {
        if (!controller.TryGetEndpoints(out Vector2 start, out Vector2 end)) return;
        Bounds bounds = new Bounds(controller.NormalizedToWorld(start), Vector3.zero);
        bounds.Encapsulate(controller.NormalizedToWorld(end));
        for (int i = 1; i < 9; i++)
        {
            bounds.Encapsulate(controller.NormalizedToWorld(controller.LeftRoute.GetPoint(i, start, end, false)));
            bounds.Encapsulate(controller.NormalizedToWorld((controller.MirrorRight ? controller.LeftRoute : controller.RightRoute).GetPoint(i, start, end, controller.MirrorRight)));
        }
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) view = EditorWindow.GetWindow<SceneView>();
        view.in2DMode = true;
        view.Frame(bounds, false);
    }
}
