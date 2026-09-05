using System.Collections;
using UnityEngine;

public class simple : MonoBehaviour
{
    public Astar astar;
    public float moveSpeed = 5f;

    void Start()
    {
        // 1. 현재 위치를 시작점으로 설정하고 길찾기 실행
        astar.startPos = transform.position;
        astar.pathFinding();

        // 2. 경로 이동 시작
        if (astar.FinalNodeList != null && astar.FinalNodeList.Count > 0)
            StartCoroutine(FollowPath());
    }

    IEnumerator FollowPath()
    {
        foreach (var node in astar.FinalNodeList)
        {
            Vector3 target = new(node.x, transform.position.y, node.z);

            // 해당 노드 위치에 근접할 때까지 이동
            while (Vector3.Distance(transform.position, target) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
                yield return null;
            }
        }
    }
}