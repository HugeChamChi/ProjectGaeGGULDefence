using Cysharp.Threading.Tasks;
using UnityEngine;

public class Test_Initalizer : MonoBehaviour
{
    void Start()
    {
        User.InitializeAsync().Forget();
    }
}
