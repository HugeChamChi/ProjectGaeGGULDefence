using BackEnd;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class BackendProfileData
{
    private static BackendProfileData instance = null;
    public static BackendProfileData Instance
    {
        get
        {
            if (instance == null)
                instance = new BackendProfileData();

            return instance;
        }
    }

    internal static UserProfileData userProfileData;

    private const string PROFILE_TABLE = "PROFILE_DATA";
    private string gameDataRowInDate = string.Empty;

    public void GetProfileData()
    {
        var bro = Backend.GameData.GetMyData(PROFILE_TABLE, new Where());
        if (bro.IsSuccess())
        {
            LitJson.JsonData gameDataJson = bro.FlattenRows();

            if (gameDataJson.Count <= 0)
            {
                Debug.LogWarning("데이터 없음");
            }
            else
            {
                gameDataRowInDate = gameDataJson[0]["inDate"].ToString();  

                userProfileData = new UserProfileData();

                userProfileData.level = int.Parse(gameDataJson[0]["level"].ToString());
                userProfileData.nickName = gameDataJson[0]["nickName"].ToString();
                userProfileData.frameID = int.Parse(gameDataJson[0]["frameID"].ToString());
                userProfileData.iconID = int.Parse(gameDataJson[0]["iconID"].ToString());

                foreach (LitJson.JsonData frameKey in gameDataJson[0]["unLockFrameHash"])
                {
                    userProfileData.unLockFrameHash.Add(int.Parse(frameKey.ToString()));
                }

                foreach (LitJson.JsonData iconKey in gameDataJson[0]["unLockIconHash"])
                {
                    userProfileData.unLockIconHash.Add(int.Parse(iconKey.ToString()));
                }

                Debug.Log(userProfileData.ToString());
            }
        }
        else
        {
            Debug.LogError("조회 실패 : " + bro);
        }
    }

    public void InsertProfileData()
    {
        if (userProfileData == null)
        {
            userProfileData = new UserProfileData();
        }

        userProfileData.level = 1;
        userProfileData.nickName = "Test1";
        userProfileData.frameID = 0;
        userProfileData.iconID = 0;

        userProfileData.unLockFrameHash.Add(0);
        userProfileData.unLockIconHash.Add(0);

        //Param => backend 서버와 통신시 사용
        Param param = new Param();
        param.Add("level", userProfileData.level);
        param.Add("nickName", userProfileData.nickName);
        param.Add("frameID", userProfileData.frameID);
        param.Add("iconID", userProfileData.iconID);
        param.Add("unLockFrameHash", userProfileData.unLockFrameHash);
        param.Add("unLockIconHash", userProfileData.unLockIconHash);

        BackendReturnObject bro = Backend.GameData.Insert(PROFILE_TABLE, param);

        if (bro.IsSuccess())
        {
            gameDataRowInDate = bro.GetInDate();
        }
        else
        {
            Debug.LogError("게임 정보 데이터 삽입 실패 : " + bro);
        }
    }

    public void UpdateProfile()
    {
        if (userProfileData == null)
        {
            Debug.LogError("데이터 없음");
            return;
        }

        Param param = new Param();
        param.Add("level", userProfileData.level);
        param.Add("nickName", userProfileData.nickName);
        param.Add("frameID", userProfileData.frameID);
        param.Add("iconID", userProfileData.iconID);
        param.Add("unLockFrameHash", userProfileData.unLockFrameHash);
        param.Add("unLockIconHash", userProfileData.unLockIconHash);

        BackendReturnObject bro = null;

        if (string.IsNullOrEmpty(gameDataRowInDate))
        {
            Debug.Log("업데이트");

            bro = Backend.GameData.Update(PROFILE_TABLE, new Where(), param);
        }
        else
        {
            bro = Backend.GameData.UpdateV2(PROFILE_TABLE, gameDataRowInDate, Backend.UserInDate, param);
        }

    }
}
