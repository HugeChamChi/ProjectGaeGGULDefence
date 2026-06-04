using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SceneNavigatorWindow : EditorWindow
{
    private Vector2 scrollPosFavorites;
    private Vector2 scrollPosAll;
    private string searchQuery = "";
    private List<SceneInfo> allScenes = new List<SceneInfo>();
    private List<SceneInfo> favoriteScenes = new List<SceneInfo>();

    private float favoritesHeight = 200f;
    private bool isResizing = false;

    private class SceneInfo
    {
        public string Name;
        public string Path;
        public bool IsFavorite;
    }

    [MenuItem("Tools/GaeGGUL/Scene Navigator", false, 0)]
    public static void ShowWindow()
    {
        GetWindow<SceneNavigatorWindow>("Scene Navigator");
    }

    private void OnEnable()
    {
        RefreshScenes();
    }

    private void RefreshScenes()
    {
        allScenes.Clear();
        favoriteScenes.Clear();

        // Assets 폴더 내의 모든 씬 검색
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        string favPref = EditorPrefs.GetString("GaeGGUL_FavoriteScenes", "");
        HashSet<string> favSet = new HashSet<string>(favPref.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;
            
            // Imports 폴더 내 씬 제외
            if (path.StartsWith("Assets/Imports")) continue;

            bool isFav = favSet.Contains(path);
            var info = new SceneInfo { Name = System.IO.Path.GetFileNameWithoutExtension(path), Path = path, IsFavorite = isFav };
            
            allScenes.Add(info);
            if (isFav) favoriteScenes.Add(info);
        }

        // 이름순 정렬
        allScenes = allScenes.OrderBy(s => s.Name).ToList();
        favoriteScenes = favoriteScenes.OrderBy(s => s.Name).ToList();
    }

    private void SaveFavorites()
    {
        var favPaths = favoriteScenes.Select(s => s.Path).ToArray();
        EditorPrefs.SetString("GaeGGUL_FavoriteScenes", string.Join(";", favPaths));
    }

    private void ToggleFavorite(SceneInfo scene)
    {
        scene.IsFavorite = !scene.IsFavorite;
        if (scene.IsFavorite)
        {
            if (!favoriteScenes.Contains(scene)) favoriteScenes.Add(scene);
        }
        else
        {
            favoriteScenes.Remove(scene);
        }
        favoriteScenes = favoriteScenes.OrderBy(s => s.Name).ToList();
        SaveFavorites();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        // 유니티 내장 검색창 스타일
        searchQuery = EditorGUILayout.TextField("", searchQuery, "SearchTextField");
        if (GUILayout.Button("Clear", "SearchCancelButtonEmpty"))
        {
            searchQuery = "";
            GUI.FocusControl(null);
        }
        
        if (GUILayout.Button("Refresh", GUILayout.Width(60)))
        {
            RefreshScenes();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        DrawFavorites();
        
        // 크기 조절용 스플리터 (Splitter)
        if (favoriteScenes.Count > 0)
        {
            GUILayout.Box("", GUILayout.Height(4), GUILayout.ExpandWidth(true));
            Rect splitterRect = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);
            
            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
            {
                isResizing = true;
                Event.current.Use();
            }
            if (isResizing && Event.current.type == EventType.MouseDrag)
            {
                favoritesHeight += Event.current.delta.y;
                favoritesHeight = Mathf.Clamp(favoritesHeight, 50f, position.height - 150f);
                Repaint();
            }
            if (Event.current.type == EventType.MouseUp)
            {
                isResizing = false;
            }
        }
        else
        {
            GUILayout.Space(10);
        }

        DrawAllScenes();
    }

    private void DrawFavorites()
    {
        GUILayout.Label("⭐ 즐겨찾는 씬 (Favorites)", EditorStyles.boldLabel);
        if (favoriteScenes.Count == 0)
        {
            GUILayout.Label("등록된 즐겨찾기가 없습니다. (별을 눌러 추가하세요)", EditorStyles.helpBox);
            return;
        }

        scrollPosFavorites = GUILayout.BeginScrollView(scrollPosFavorites, GUILayout.Height(favoritesHeight));
        foreach (var scene in favoriteScenes)
        {
            if (!string.IsNullOrEmpty(searchQuery) && !scene.Name.ToLower().Contains(searchQuery.ToLower()))
                continue;
            DrawSceneRow(scene);
        }
        GUILayout.EndScrollView();
    }

    private void DrawAllScenes()
    {
        GUILayout.Label("📁 모든 씬 (All Scenes)", EditorStyles.boldLabel);
        
        scrollPosAll = GUILayout.BeginScrollView(scrollPosAll);
        foreach (var scene in allScenes)
        {
            // 즐겨찾기에 있는 씬은 아래 목록에서는 제외
            if (scene.IsFavorite) continue; 
            
            if (!string.IsNullOrEmpty(searchQuery) && !scene.Name.ToLower().Contains(searchQuery.ToLower()))
                continue;
                
            DrawSceneRow(scene);
        }
        GUILayout.EndScrollView();
    }

    private void DrawSceneRow(SceneInfo scene)
    {
        GUILayout.BeginHorizontal("box");
        
        // 즐겨찾기 버튼
        string favIcon = scene.IsFavorite ? "★" : "☆";
        GUIStyle favStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        favStyle.normal.textColor = scene.IsFavorite ? Color.yellow : Color.gray;
        
        if (GUILayout.Button(favIcon, favStyle, GUILayout.Width(30), GUILayout.Height(30)))
        {
            ToggleFavorite(scene);
        }

        // 씬 이름 및 경로
        GUILayout.BeginVertical();
        GUILayout.Label(scene.Name, EditorStyles.boldLabel);
        
        GUIStyle pathStyle = new GUIStyle(EditorStyles.miniLabel);
        pathStyle.normal.textColor = Color.gray;
        GUILayout.Label(scene.Path, pathStyle);
        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        // 열기 버튼
        if (GUILayout.Button("Open", GUILayout.Width(60), GUILayout.Height(30)))
        {
            OpenScene(scene.Path);
        }

        GUILayout.EndHorizontal();
    }

    private void OpenScene(string path)
    {
        // 현재 씬이 수정되었다면 저장할지 묻는 팝업 띄우기
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(path);
        }
    }
}
