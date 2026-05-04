using System;
using BackEnd;
using UnityEngine;

namespace GaeGGUL.Core
{
    public static class Server
    {
        public static DateTime GetServerTime()
        {
            var bro = Backend.Utils.GetServerTime();
            if (bro.IsSuccess())
            {
                string timeStr = bro.GetReturnValuetoJSON()["utcTime"].ToString();
                return DateTime.Parse(timeStr).ToUniversalTime();
            }

            Debug.LogWarning("[Server] 서버 시간을 불러오지 못했습니다. 로컬 시간을 사용합니다.");
            return DateTime.UtcNow;
        }

        public static DateTime ToKST(DateTime utcTime)
        {
            return utcTime.AddHours(9);
        }
    }
}
