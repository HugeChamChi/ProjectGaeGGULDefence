using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class PlayerCharacterManager
{
    private Dictionary<int, int> _ownedCharacterCounts = new Dictionary<int, int>();

    const string TABLE_NAME = "PlayerOwnedCharacterData";
    private string rowInDate = string.Empty;

    public async UniTask InitalizeAsync()
    {
        bool isInit = false;

        Load(() => isInit = true);

        await UniTask.WaitUntil(() => isInit);
    }

    public int GetOwnedCount(int characterId)
    {
        return _ownedCharacterCounts.TryGetValue(characterId, out int count) ? count : 0;
    }

    public void GetCharacter(int characterId)
    {
        if (_ownedCharacterCounts.ContainsKey(characterId))
        {
            _ownedCharacterCounts[characterId]++;
        }
        else
        {
            _ownedCharacterCounts[characterId] = 1;
        }
    }
}