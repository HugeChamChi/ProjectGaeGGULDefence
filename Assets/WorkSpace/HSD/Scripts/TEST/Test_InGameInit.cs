using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class Test_InGameInit : MonoBehaviour
{
    private void Start()                                                                                   
    {                                         
        var rootContainer = LifetimeScope.Find<RootLifetimeScope>().Container;
        var gameDataManager = rootContainer.Resolve<GameDataManager>();

        // 로드 실행                                                                                                                                
        gameDataManager.LoadAllAsync().Forget();
    }
}
