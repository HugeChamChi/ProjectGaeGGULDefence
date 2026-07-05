using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.Networking;
using UnityEditor.Localization;

namespace HSD.Editor
{
    public class GoogleSheetsLocalizationImporter : EditorWindow
    {
        private const string PrefKey_SheetId = "GSLI_SheetId";
        private const string PrefKey_CommonGid = "GSLI_CommonGid";
        private const string PrefKey_DataGid = "GSLI_DataGid";

        private string sheetId = "";
        private string commonGid = "";
        private string dataGid = "";

        [MenuItem("Tools/Localization/Google Sheets Importer")]
        public static void ShowWindow()
        {
            GetWindow<GoogleSheetsLocalizationImporter>("Google Sheets Importer");
        }

        private void OnEnable()
        {
            sheetId = EditorPrefs.GetString(PrefKey_SheetId, "");
            commonGid = EditorPrefs.GetString(PrefKey_CommonGid, "");
            dataGid = EditorPrefs.GetString(PrefKey_DataGid, "");
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(PrefKey_SheetId, sheetId);
            EditorPrefs.SetString(PrefKey_CommonGid, commonGid);
            EditorPrefs.SetString(PrefKey_DataGid, dataGid);
        }

        private void OnGUI()
        {
            GUILayout.Label("Google Sheets Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            
            sheetId = EditorGUILayout.TextField("Spreadsheet ID", sheetId);
            commonGid = EditorGUILayout.TextField("Common Table GID", commonGid);
            dataGid = EditorGUILayout.TextField("Data Table GID", dataGid);

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PrefKey_SheetId, sheetId);
                EditorPrefs.SetString(PrefKey_CommonGid, commonGid);
                EditorPrefs.SetString(PrefKey_DataGid, dataGid);
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Pull Data", GUILayout.Height(30)))
            {
                PullData();
            }
        }

        private async void PullData()
        {
            if (string.IsNullOrEmpty(sheetId))
            {
                Debug.LogError("[GoogleSheetsImporter] Spreadsheet ID is empty.");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Google Sheets Importer", "Downloading Common Table...", 0.2f);
                if (!string.IsNullOrEmpty(commonGid))
                {
                    await DownloadAndApplyTable("Common", commonGid);
                }

                EditorUtility.DisplayProgressBar("Google Sheets Importer", "Downloading Data Table...", 0.6f);
                if (!string.IsNullOrEmpty(dataGid))
                {
                    await DownloadAndApplyTable("Data", dataGid);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            
            AssetDatabase.SaveAssets();
            Debug.Log("[GoogleSheetsImporter] Done! Saved assets.");
        }

        private async Task DownloadAndApplyTable(string tableName, string gid)
        {
            string url = $"https://docs.google.com/spreadsheets/d/{sheetId}/export?format=csv&gid={gid}";
            
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                var operation = www.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[GoogleSheetsImporter] Failed to download {tableName} table. Error: {www.error}");
                    return;
                }

                string csvData = www.downloadHandler.text;
                if (string.IsNullOrEmpty(csvData))
                {
                    Debug.LogWarning($"[GoogleSheetsImporter] CSV data for {tableName} is empty.");
                    return;
                }

                ApplyCSVData(tableName, csvData);
            }
        }

        private void ApplyCSVData(string targetTableName, string csvData)
        {
            var lines = ParseCSV(csvData);
            if (lines.Count < 2)
            {
                Debug.LogError($"[GoogleSheetsImporter] The CSV for {targetTableName} doesn't seem to have enough rows (Header + Data).");
                return;
            }

            // Find StringTableCollection
            var tableCollection = FindTableCollection(targetTableName);
            if (tableCollection == null)
            {
                Debug.LogError($"[GoogleSheetsImporter] Could not find StringTableCollection with name: {targetTableName}");
                return;
            }

            string[] headers = lines[0];
            
            // Match header string with StringTable
            var localeTableMap = new Dictionary<int, StringTable>();
            for (int i = 1; i < headers.Length; i++)
            {
                string localeCode = headers[i].Trim();
                if (string.IsNullOrEmpty(localeCode)) continue;

                var table = tableCollection.GetTable(localeCode) as StringTable;
                if (table == null)
                {
                    // Try finding by localeIdentifier code
                    foreach (var t in tableCollection.StringTables)
                    {
                        if (t.LocaleIdentifier.Code.Equals(localeCode, System.StringComparison.OrdinalIgnoreCase))
                        {
                            table = t;
                            break;
                        }
                    }
                }

                if (table != null)
                {
                    localeTableMap.Add(i, table);
                }
                else
                {
                    Debug.LogWarning($"[GoogleSheetsImporter] Locale '{localeCode}' not found in StringTableCollection '{targetTableName}'.");
                }
            }

            // Apply Entries
            for (int row = 1; row < lines.Count; row++)
            {
                string[] columns = lines[row];
                if (columns.Length == 0) continue;

                string key = columns[0].Trim();
                if (string.IsNullOrEmpty(key)) continue;

                // Add or Update Entry in Shared Table Data
                var sharedEntry = tableCollection.SharedData.GetEntry(key);
                if (sharedEntry == null)
                {
                    sharedEntry = tableCollection.SharedData.AddKey(key);
                }

                foreach (var kvp in localeTableMap)
                {
                    int colIndex = kvp.Key;
                    StringTable table = kvp.Value;

                    if (colIndex < columns.Length)
                    {
                        string value = columns[colIndex];
                        var entry = table.GetEntry(sharedEntry.Id);
                        if (entry != null)
                        {
                            entry.Value = value;
                        }
                        else
                        {
                            table.AddEntry(key, value);
                        }
                        EditorUtility.SetDirty(table);
                    }
                }
                EditorUtility.SetDirty(tableCollection.SharedData);
            }
            EditorUtility.SetDirty(tableCollection);
        }

        private StringTableCollection FindTableCollection(string tableName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:StringTableCollection {tableName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
                if (collection != null && collection.name.Contains(tableName))
                {
                    return collection;
                }
            }
            return null;
        }

        // A simple CSV parser handling quoted strings and newlines within fields
        private List<string[]> ParseCSV(string text)
        {
            List<string[]> list = new List<string[]>();
            bool inQuotes = false;
            List<string> currentRow = new List<string>();
            string currentField = "";

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\"')
                {
                    if (inQuotes && i + 1 < text.Length && text[i + 1] == '\"')
                    {
                        // Escaped quote
                        currentField += '\"';
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    currentRow.Add(currentField);
                    currentField = "";
                }
                else if ((c == '\r' || c == '\n') && !inQuotes)
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++; // Skip \n in \r\n
                    }
                    
                    currentRow.Add(currentField);
                    list.Add(currentRow.ToArray());
                    currentRow = new List<string>();
                    currentField = "";
                }
                else
                {
                    currentField += c;
                }
            }

            if (currentRow.Count > 0 || currentField.Length > 0)
            {
                currentRow.Add(currentField);
                list.Add(currentRow.ToArray());
            }

            return list;
        }
    }
}
