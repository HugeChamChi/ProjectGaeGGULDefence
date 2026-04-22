using UnityEngine;

// 그리드가 깔릴 영역을 씬에서 시각적으로 표시 (Gizmo만 사용, 배경 없음)
public class GridArea : MonoBehaviour
{
    [Header("월드 유닛 기준 (픽셀 아님!)")]
    [SerializeField] public Vector2 size = new Vector2(8f, 5f);

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawCube(transform.position, new Vector3(size.x, 0.01f, size.y));
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.01f, size.y));
    }
}