namespace Global
{
    public interface IClearable
    {
        /// <summary>
        /// 로그아웃 혹은 계정 변경 시 로컬 메모리의 데이터를 초기화합니다.
        /// </summary>
        void Clear();

        /// <summary>
        /// 데이터가 변경되어 서버에 저장해야 하는지 여부
        /// </summary>
        bool IsDirty { get; set; }

        /// <summary>
        /// IsDirty가 true일 때 데이터를 뒤끝 DB에 부분 업데이트합니다.
        /// </summary>
        Cysharp.Threading.Tasks.UniTask SaveAsync();
    }
}
