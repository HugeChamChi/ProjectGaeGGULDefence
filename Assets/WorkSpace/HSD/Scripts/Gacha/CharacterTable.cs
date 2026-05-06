using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class CharacterTable
{
    private Dictionary<int, ICharacterData> _charDict = new Dictionary<int, ICharacterData>();
    public IEnumerable<ICharacterData> Characters => _charDict.Values;
    public int Count => _charDict.Count;

    const string PATH = "Data/CharacterData";

    public async UniTask InitializeAsync()
    {
        var charDataList = await RM.LoadAllAsync<Test_CharacterData>(PATH);

        foreach (var charData in charDataList)
        {
            _charDict.TryAdd(charData.Id, charData);
        }
    }

    public ICharacterData GetCharacterData(int id)
    {
        if (_charDict.TryGetValue(id, out var data))
            return data;
        else
            throw new Exception($"캐릭터 데이터가 존재하지 않습니다. id: {id}");
    }
}
