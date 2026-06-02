namespace Global
{
    public interface IClearable
    {
        /// <summary>
        /// 로그아웃 혹은 계정 변경 시 로컬 메모리의 데이터를 초기화합니다.
        /// </summary>
        void Clear();
    }
}
