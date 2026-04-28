using UnityEngine;

namespace WorkSpace.HSD.Test
{
    /// <summary>
    /// ProfileManager의 저장 및 로드 기능을 테스트하기 위한 스크립트입니다.
    /// 인스펙터 창에서 컴포넌트 우클릭 메뉴(Test Save, Test Load)를 사용하거나 
    /// 재생 중 키보드 1, 2번을 눌러 테스트할 수 있습니다.
    /// </summary>
    public class Test_ProfileManager : MonoBehaviour
    {
        private ProfileManager _profileManager;

        private void Start()
        {
            _profileManager = new ProfileManager();
            Debug.Log("<color=yellow>ProfileManager 테스트 스크립트 시작</color>");
            Debug.Log("<b>[1]</b>: 더미 데이터 저장 테스트");
            Debug.Log("<b>[2]</b>: 서버 데이터 로드 테스트");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Test_Save();
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                Test_Load();
            }
        }

        [ContextMenu("Test Save")]
        public void Test_Save()
        {
            Debug.Log("<b>[저장 테스트]</b> 더미 데이터를 설정하고 서버에 저장을 시도합니다...");
            
            // 테스트용 데이터 설정
            _profileManager.Data.iconStatusMap.Clear();
            _profileManager.Data.iconStatusMap.Add("Frog_Icon_01", true);
            _profileManager.Data.iconStatusMap.Add("Frog_Icon_02", false);

            _profileManager.Data.frameStatusMap.Clear();
            _profileManager.Data.frameStatusMap.Add("Basic_Frame", true);
            _profileManager.Data.frameStatusMap.Add("VIP_Frame", false);

            _profileManager.Data.CurrentIconKey = "Frog_Icon_01";

            _profileManager.Save();
        }

        [ContextMenu("Test Load")]
        public void Test_Load()
        {
            Debug.Log("<b>[로드 테스트]</b> 서버로부터 데이터를 불러옵니다...");
            _profileManager.Load();
        }
    }
}
