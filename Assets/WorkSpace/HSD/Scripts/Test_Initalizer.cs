using Cysharp.Threading.Tasks;
using UnityEngine;

public class Test_Initalizer : MonoBehaviour
{
    void Start()
    {
        Player.InitializeAsync().Forget();
    }
}
