using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class ItemTable
{
    private Dictionary<int, IItemData> _itemDict = new Dictionary<int, IItemData>();
    public IEnumerable<IItemData> Items => _itemDict.Values;
    public int Count => _itemDict.Count;

    const string PATH = "Data/ItemData";

    public async UniTask InitializeAsync()
    {
        var itemDataList = await RM.LoadAllAsync<ItemData>(PATH);

        foreach (var itemData in itemDataList)
        {
            _itemDict.TryAdd(itemData.Id, itemData);
        }
    }

    public IItemData Get(int id)
    {
        if (_itemDict.TryGetValue(id, out var data))
            return data;
        else
            throw new Exception($"아이템 데이터가 존재하지 않습니다. id: {id}");
    }
}
