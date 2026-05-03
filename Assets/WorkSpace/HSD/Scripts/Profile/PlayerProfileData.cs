using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using UnityEngine;

public class PlayerProfileData
{
    public Dictionary<string, bool> iconStatusMap = new();
    public Dictionary<string, bool> frameStatusMap = new();
    public int CurrentIconId = 0;
    public int CurrentFrameId = 0;

    const string COLUMN_PROFILE_ICON_DATA = "IconData";
    const string COLUMN_PROFILE_FRAME_DATA = "FrameData";
    const string COLUMN_PROFILE_CURRENT_ICON = "CurrentIconData";
    const string COLUMN_PROFILE_CURRENT_FRAME = "CurrentFrameData";

    // 뒤끝 저장을 위한 Param 변환 헬퍼 메서드
    public Param ToParam()
    {
        Param param = new Param();
        param.Add(COLUMN_PROFILE_ICON_DATA, iconStatusMap);
        param.Add(COLUMN_PROFILE_FRAME_DATA, frameStatusMap);
        param.Add(COLUMN_PROFILE_CURRENT_ICON, CurrentIconId);
        param.Add(COLUMN_PROFILE_CURRENT_FRAME, CurrentFrameId);
        return param;
    }

    // 뒤끝 JsonData를 객체 데이터로 복구하는 헬퍼 메서드
    public void FromData(JsonData data)
    {
        try
        {
            // IconData 복구
            if (data.ContainsKey(COLUMN_PROFILE_ICON_DATA) && data[COLUMN_PROFILE_ICON_DATA] != null)
            {
                iconStatusMap.Clear();
                JsonData iconData = data[COLUMN_PROFILE_ICON_DATA];
                foreach (string key in iconData.Keys)
                {
                    iconStatusMap.Add(key, bool.Parse(iconData[key].ToString()));
                }
            }

            // FrameData 복구
            if (data.ContainsKey(COLUMN_PROFILE_FRAME_DATA) && data[COLUMN_PROFILE_FRAME_DATA] != null)
            {
                frameStatusMap.Clear();
                JsonData frameData = data[COLUMN_PROFILE_FRAME_DATA];
                foreach (string key in frameData.Keys)
                {
                    frameStatusMap.Add(key, bool.Parse(frameData[key].ToString()));
                }
            }

            // CurrentIconData 복구
            if (data.ContainsKey(COLUMN_PROFILE_CURRENT_ICON) && data[COLUMN_PROFILE_CURRENT_ICON] != null)
            {
                CurrentIconId = (int)data[COLUMN_PROFILE_CURRENT_ICON];
            }

            // CurrentFrameData 복구
            if (data.ContainsKey(COLUMN_PROFILE_CURRENT_FRAME) && data[COLUMN_PROFILE_CURRENT_FRAME] != null)
            {
                CurrentFrameId = (int)data[COLUMN_PROFILE_CURRENT_FRAME];
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"프로필 데이터 파싱 중 예외 발생: {e.Message}");
        }
    }
}
