using System.Collections.Generic;
using Cysharp.Threading.Tasks;
// -------------------------
// 데이터 로더 인터페이스 및 구현
// -------------------------
public interface IDataProvider<T>
{
    UniTask<Dictionary<int, T>> LoadAsync();
}
