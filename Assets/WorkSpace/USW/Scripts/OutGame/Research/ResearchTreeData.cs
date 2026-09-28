using System.Collections.Generic;
using UnityEngine;

/// <summary>강화 트리 하나 (구성별로 하나씩). 노드 배치·연결·수치·해금 조건을 모두 여기서 정한다.</summary>
[CreateAssetMenu(fileName = "UpgradeTree", menuName = "USW/OutGame/Upgrade Tree Data")]
public class ResearchTreeData : ScriptableObject
{
    /// <summary>저장과 엑셀의 고정 키. 파일명/표시명 변경 시에도 변경하지 않는다.</summary>
    public string TreeKey;
    /// <summary>무버전 저장 이관용 기존 파일명. 새 트리는 비우며 파일명 변경 시에도 유지한다.</summary>
    public string LegacySaveName;
    [Tooltip("테스트 버튼에 보일 구성 이름")]
    public string DisplayName;
    [Tooltip("true면 맨 아래에 줄기 이름을 쓴다 (3줄기 구성)")]
    public bool ShowLanes;
    [Tooltip("왼쪽/오른쪽 칸이 가운데에서 떨어진 거리. 0이면 화면 설정 값을 쓴다")]
    public float ColumnSpacing;
    [Tooltip("줄기 이름 (왼쪽, 가운데, 오른쪽)")]
    public string[] LaneNames = new string[3];
    public List<ResearchNodeData> Nodes = new List<ResearchNodeData>();

    /// <summary>Id로 노드를 찾는다. 없으면 null.</summary>
    public ResearchNodeData Find(string id)
    {
        foreach (var node in Nodes)
            if (node != null && node.Id == id) return node;
        return null;
    }
}
