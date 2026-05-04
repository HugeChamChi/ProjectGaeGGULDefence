using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Test_Initalizer : MonoBehaviour
{
    private void Start()
    {
        StartAsync().Forget();
    }

    private async UniTask StartAsync()
    {
        await UniTask.WaitForSeconds(0.1f);
        Table.InitializeAsync().Forget();
        Player.InitializeAsync().Forget();
    }
}
