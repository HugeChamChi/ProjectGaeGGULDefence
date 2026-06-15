using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class TotemImageAssigner : EditorWindow
{
    [MenuItem("Tools/임시 토템 이미지 자동 세팅")]
    public static void AssignRandomImages()
    {
        string imageFolder = "Assets/Imports/Totem";
        string soFolder = "Assets/WorkSpace/USW/Data/TotemData";

        // 1. 이미지 폴더 내의 모든 스프라이트 가져오기
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { imageFolder });
        var idToSprites = new Dictionary<string, Sprite[]>();

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            
            // 이름 형태: Totem_001, Totem_001_90_L, Totem_001_180, Totem_001_90_R 등
            if (name.StartsWith("Totem_") && name.Length >= 9)
            {
                string id = name.Substring(6, 3); // "001", "002" 등 추출
                
                if (!idToSprites.ContainsKey(id))
                {
                    idToSprites[id] = new Sprite[4];
                }

                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                // 규칙 매칭
                if (name == $"Totem_{id}") 
                {
                    idToSprites[id][0] = s; // 0도 (기본)
                }
                else if (name.Contains("90_R") || name.Contains("90__R")) 
                {
                    idToSprites[id][1] = s; // 90도 (우회전이라 가정, 시계방향 90도)
                }
                else if (name.Contains("180")) 
                {
                    idToSprites[id][2] = s; // 180도
                }
                else if (name.Contains("90_L") || name.Contains("90__L")) 
                {
                    idToSprites[id][3] = s; // 270도 (좌회전, 시계방향 270도)
                }
            }
        }

        // 완성된 이미지 세트(4개의 스프라이트가 모두 있는 경우)만 필터링
        var availableIds = idToSprites.Keys.Where(k => idToSprites[k][0] != null).ToList();
        
        if (availableIds.Count == 0)
        {
            Debug.LogError("적용할 토템 이미지를 찾을 수 없습니다.");
            return;
        }

        // 2. SO 찾아서 랜덤하게 세팅
        string[] soGuids = AssetDatabase.FindAssets("t:TotemData", new[] { soFolder });
        
        int assignedCount = 0;
        foreach (var guid in soGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TotemData data = AssetDatabase.LoadAssetAtPath<TotemData>(path);
            if (data != null)
            {
                // 랜덤하게 001~025 중 하나의 세트 선택
                string randomId = availableIds[Random.Range(0, availableIds.Count)];
                Sprite[] sprites = idToSprites[randomId];

                // 데이터 세팅
                data.icon = sprites[0];
                
                if (data.rotationSprites == null || data.rotationSprites.Length != 4)
                {
                    data.rotationSprites = new Sprite[4];
                }
                
                data.rotationSprites[0] = sprites[0];
                data.rotationSprites[1] = sprites[1];
                data.rotationSprites[2] = sprites[2];
                data.rotationSprites[3] = sprites[3];

                EditorUtility.SetDirty(data);
                assignedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"총 {assignedCount}개의 TotemData에 랜덤으로 임시 이미지를 세팅했습니다!");
    }
}
