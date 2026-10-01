using UnityEngine;
using UnityEngine.UI;

public class TitleView : MonoBehaviour
{
    [Header("Title UI")]
    [Tooltip("로비로 넘어가는 버튼 (할당하지 않으면 화면 터치 사용)")]
    public Button startButton;
    [SerializeField] private TMPro.TMP_Text _status;
    /// <summary>로그인 진행 및 재시도 안내를 표시한다.</summary>
    public void SetStatus(string message) { if (_status != null) _status.text = message; }
    
    // 차후 텍스트나 로고 연출 등이 필요하다면 여기에 추가로 선언합니다.
}
