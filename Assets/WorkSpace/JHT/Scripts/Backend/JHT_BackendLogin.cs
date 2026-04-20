using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JHT_BackendLogin : MonoBehaviour
{
    private static JHT_BackendLogin instance = null;

    public static JHT_BackendLogin Instance
    {
        get
        {
            if (instance == null)
                instance = new JHT_BackendLogin();

            return instance;
        }
    }

    // 회원가입 구현 로직
    public void CustomSignUp(string id, string pw)
    {
        if (id == "" || pw == "")
            return;
        Debug.Log("회원가입을 요청합니다.");

        var bro = Backend.BMember.CustomSignUp(id, pw);

        if (bro.IsSuccess())
            Debug.Log($"회원가입을 성공했습니다 : {bro}");
        else
            Debug.LogError($"회원가입에 실패햇습니다 : {bro}");

    }

    // 로그인 구현 로직
    public BackendReturnObject CustomLogin(string id, string pw)
    {
        if (id == "" || pw == "")
            return null;

        Debug.Log("로그인 요청");

        var bro = Backend.BMember.CustomLogin(id, pw);

        if (bro.IsSuccess())
        {
            Debug.Log("로그인 성공 : " + bro);
            return bro;
        }
        else
        {
            Debug.LogError("로그인 실패 : " + bro);
            return null;
        }
    }

    // 닉네임 변경 구현 로직
    public void UpdateNickname(string nickname)
    {
        Debug.Log("닉네임 변경을 요청합니다.");

        var bro = Backend.BMember.UpdateNickname(nickname);

        if (bro.IsSuccess())
        {
            Debug.Log("닉네임 변경에 성공했습니다 : " + bro);
        }
        else
        {
            Debug.LogError("닉네임 변경에 실패했습니다 : " + bro);
        }
    }
}
