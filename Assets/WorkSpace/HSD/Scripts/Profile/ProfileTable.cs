using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class ProfileTable
{
    Dictionary<string, IProfileItem> profileItems = new();
    Dictionary<string, IProfileItem> frameItems = new();

    public async UniTask InitializeAsync()
    {
        
    }
}
