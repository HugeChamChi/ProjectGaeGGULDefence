using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using UnityEngine;

public class ProfileData
{
    public Dictionary<string, bool> iconStatusMap = new();
    public Dictionary<string, bool> frameStatusMap = new();
    public string CurrentIconKey = string.Empty;
    public string CurrentFrameKey = string.Empty;

    const string IconDataKey = "IconData";
    const string FrameDataKey = "FrameData";
    const string CurrentIconDataKey = "CurrentIconData";
    const string CurrentFrameDataKey = "CurrentFrameData";

    // 뒤끝 저장을 위한 Param 변환 헬퍼 메서드
    public Param ToParam()
    {
        Param param = new Param();
        param.Add(IconDataKey, iconStatusMap);
        param.Add(FrameDataKey, frameStatusMap);
        param.Add(CurrentIconDataKey, CurrentIconKey);
        param.Add(CurrentFrameDataKey, CurrentFrameKey);
        return param;
    }

    // 뒤끝 JsonData를 객체 데이터로 복구하는 헬퍼 메서드
    public void FromData(JsonData data)
    {
        try
        {
            // IconData 복구
            if (data.ContainsKey(IconDataKey) && data[IconDataKey] != null)
            {
                iconStatusMap.Clear();
                JsonData iconData = data[IconDataKey];
                foreach (string key in iconData.Keys)
                {
                    iconStatusMap.Add(key, bool.Parse(iconData[key].ToString()));
                }
            }

            // FrameData 복구
            if (data.ContainsKey(FrameDataKey) && data[FrameDataKey] != null)
            {
                frameStatusMap.Clear();
                JsonData frameData = data[FrameDataKey];
                foreach (string key in frameData.Keys)
                {
                    frameStatusMap.Add(key, bool.Parse(frameData[key].ToString()));
                }
            }

            // CurrentIconData 복구
            if (data.ContainsKey(CurrentIconDataKey) && data[CurrentIconDataKey] != null)
            {
                CurrentIconKey = data[CurrentIconDataKey].ToString();
            }

            // CurrentFrameData 복구
            if (data.ContainsKey(CurrentFrameDataKey) && data[CurrentFrameDataKey] != null)
            {
                CurrentFrameKey = data[CurrentFrameDataKey].ToString();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"프로필 데이터 파싱 중 예외 발생: {e.Message}");
        }
    }
}
