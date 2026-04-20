using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerDataView : MonoBehaviour
{
    // 찾은 UI들 캐싱
    private Dictionary<string, List<TMP_Text>> cachedTexts = new Dictionary<string, List<TMP_Text>>();
    //[SerializeField] private TMP_Text _playerNameText;
    //[SerializeField] private TMP_Text _goldText;
    //[SerializeField] private TMP_Text _diamondText;

    private List<TMP_Text> staminaTimerTexts = new List<TMP_Text>();
    
    private List<Image> expBars = new();
    
    private void OnEnable()
    {
        if (cachedTexts.Count > 0)
        {
            cachedTexts.Clear();
        } 
        
        staminaTimerTexts.Clear();
    }
    
    private void Start()
    {
        // PlayerDataController의 스테미나 타이머 이벤트 구독
        var controller = FindObjectOfType<PlayerDataController>();
        if (controller != null)
        {
            controller.OnStaminaRecoveryTimer += UpdateStaminaTimer;
        }
    }

    public void UpdateUI(PlayerData data)
    {
        var dataType = typeof(PlayerData);
        var fields = dataType.GetFields();

        foreach (var field in fields)
        {
            var value = field.GetValue(data);
            string textValue = FormatValue(field.Name, value,data);
            UpdateTextsByName(field.Name + "Text", textValue);
        }

        UpdateExpBar(data);

        //  _playerNameText.text = data.PlayerName;
        //  _goldText.text = $"{data.Gold}";
        //  _diamondText.text = $"{data.Diamond}";
    }
    
    // 이름으로 텍스트 업데이트
    private void UpdateTextsByName(string objectName, string value)
    {
        // 캐시에서 먼저 확인
        if (!cachedTexts.ContainsKey(objectName))
        {
            var foundTexts = new List<TMP_Text>();
            var allTexts = FindObjectsOfType<TMP_Text>(true); 

            foreach (var text in allTexts)
            {
                if (text.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    foundTexts.Add(text);
                }
            }

            cachedTexts[objectName] = foundTexts;
        }

        // 찾은 모든 텍스트 업데이트
        foreach (var text in cachedTexts[objectName])
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
    
    // 스테미나 타이머 텍스트 업데이트
    private void UpdateStaminaTimer(int remainingSeconds)
    {
        if (staminaTimerTexts.Count == 0)
        {
            // 스테미나 타이머 텍스트들 찾기 
            var allTexts = FindObjectsOfType<TMP_Text>(true);
            foreach (var text in allTexts)
            {
                if (text.name.Equals("StaminaTimerText", StringComparison.OrdinalIgnoreCase))
                {
                    staminaTimerTexts.Add(text);
                }
            }
        }

        string timerText = remainingSeconds > 0 ? FormatTime(remainingSeconds) : "FULL";
        
        foreach (var text in staminaTimerTexts)
        {
            if (text != null)
            {
                text.text = timerText;
            }
        }
    }

    // 유저 경험치 게이지바 업데이트
    private void UpdateExpBar(PlayerData data)
    {
        if (expBars.Count == 0)
        {
            var allImages = FindObjectsOfType<Image>(true);
            foreach (var img in allImages)
            {
                if (img.name.Equals("TopPanel_PlayerExpBar (1)", StringComparison.OrdinalIgnoreCase))
                {
                    expBars.Add(img);
                }
            }

            if (expBars.Count == 0)
            {
                return;
            }
        }

        float ratio = 0f;
        int maxExp = data.MaxExp > 0 ? data.MaxExp : 500;
        ratio = Mathf.Clamp01((float)data.PlayerExp / maxExp);

        foreach (var bar in expBars)
        {
            if (bar != null)
            {
                bar.fillAmount = ratio;
            }
        }
    }

    // 값 포맷팅
    private string FormatValue(string fieldName, object value, PlayerData data = null)
    {
        // MaxStamina는 일단 필드에 존재하긴 하니깐 FormatValue 가 호출됨 그래서 집어는 넣어야함. 
        // return fieldName switch
        // {
        //     "Gold" => $"{value:N0}", 
        //     "Diamond" => $"{value:N0}", 
        //     "Stamina" => data != null ? $"{data.Stamina}/{data.MaxStamina}" : $"{value}",
        //     "MaxStamina" => "",
        //     _ => value?.ToString() ?? ""
        //     
        // };
        Debug.Log($"fieldName : {fieldName}, value:{value}, data==null: {data==null}");
        
        string result = fieldName switch
        {
            "Gold" => $"{value:N0}", 
            "Diamond" => $"{value:N0}", 
            "PlayerLevel" => $"{value:N0}",
            "PlayerExp" => data != null ? $"{data.PlayerExp}/{data.MaxExp}" : $"{value}",
            "Stamina" => data != null ? $"{data.Stamina}/{data.MaxStamina}" : $"{value}",
            "MaxStamina" => "",
            _ => value?.ToString() ?? "",

        };
        Debug.Log($"result: {result}");
        return result;
    }
    
    // 시간 포맷팅
    private string FormatTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:D2}:{seconds:D2}";
    }
    
    private void OnDestroy()
    {
        // 이벤트 구독 해제
        var controller = FindObjectOfType<PlayerDataController>();
        if (controller != null)
        {
            controller.OnStaminaRecoveryTimer -= UpdateStaminaTimer;
        }
    }
}