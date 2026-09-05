using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Node
{
    public Node(bool _isObstacle, float _x, float _z)
    {
        isObstacle = _isObstacle;
        x = _x;
        z = _z;
    }

    public Node ParentNode;
    public bool isObstacle;

    public float x, z, G, H;

    public float F 
    { 
        get { return G + (1.5f * H); } 
    } 
}

public class Astar : MonoBehaviour
{
    public Vector3 bottomLeft, topRight, startPos, targetPos;
    public List<Node> FinalNodeList;
    public float cellSize = 2.5f;

    int sizeX, sizeZ;
    Node[,] NodeArray;
    Node startNode, targetNode, curNode;
    List<Node> openList, closedList;

    public void pathFinding()
    {
        sizeX = Mathf.RoundToInt((topRight.x - bottomLeft.x) / cellSize + 1);
        sizeZ = Mathf.RoundToInt((topRight.z - bottomLeft.z) / cellSize + 1);
        NodeArray = new Node[sizeX, sizeZ];

        for (int i = 0; i < sizeX; i++)
        {
            for (int j = 0; j < sizeZ; j++)
            {
                float x = (i * cellSize) + bottomLeft.x;
                float z = (j * cellSize) + bottomLeft.z;

                bool isObstacle = false;
                foreach (Collider col in Physics.OverlapSphere(new Vector3(x, 0, z), 4f))
                { 
                    if (col.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
                    {
                        isObstacle = true;
                    }
                }

                NodeArray[i,j] = new Node(isObstacle, x, z);
            }
        }

        startNode = NodeArray[Mathf.RoundToInt((startPos.x - bottomLeft.x) / cellSize), Mathf.RoundToInt((startPos.z - bottomLeft.z) / cellSize)];
        targetNode = NodeArray[Mathf.RoundToInt((targetPos.x - bottomLeft.x) / cellSize), Mathf.RoundToInt((targetPos.z - bottomLeft.z) / cellSize)];

        openList = new List<Node>() { startNode };
        closedList = new List<Node>();
        FinalNodeList = new List<Node>();

        while (openList.Count > 0)
        {
            curNode = openList[0];

            for (int i = 0; i < openList.Count; i++)
            {
                if (openList[i].F <= curNode.F && openList[i].H < curNode.H)
                {
                    curNode = openList[i];
                }
            }

            openList.Remove(curNode);
            closedList.Add(curNode);

            if (curNode == targetNode)
            {
                Node targetCurNode = targetNode;

                while (targetCurNode != startNode)
                {
                    FinalNodeList.Add(targetCurNode);
                    targetCurNode = targetCurNode.ParentNode;
                }

                FinalNodeList.Add(startNode);
                FinalNodeList.Reverse();

                return;
            }

            OpenListAdd(curNode.x, curNode.z + cellSize); // 상
            OpenListAdd(curNode.x + cellSize, curNode.z); // 좌
            OpenListAdd(curNode.x, curNode.z - cellSize); // 하
            OpenListAdd(curNode.x - cellSize, curNode.z); // 우
        }
    }

    void OpenListAdd(float checkX, float checkZ)
    {
        int x = Mathf.RoundToInt((checkX - bottomLeft.x) / cellSize);
        int z = Mathf.RoundToInt((checkZ - bottomLeft.z) / cellSize);

        if (checkX >= bottomLeft.x && checkX <= topRight.x &&
            checkZ >= bottomLeft.z && checkZ <= topRight.z && !NodeArray[x, z].isObstacle && !closedList.Contains(NodeArray[x, z]))
        { 
            Node neighborNode = NodeArray[x, z];
            float moveCost = curNode.G + 10;

            if (moveCost <= neighborNode.G || !openList.Contains(neighborNode))
            {
                neighborNode.G = moveCost;
                neighborNode.H = (Mathf.Abs(neighborNode.x - targetNode.x) + Mathf.Abs(neighborNode.z - targetNode.z));
                neighborNode.ParentNode = curNode;

                openList.Add(neighborNode);
            }
        }
    }

    void OnDrawGizmos()
    {
        if (FinalNodeList.Count != 0)
        {
            for (int i = 0; i < FinalNodeList.Count - 1; i++)
            {
                Gizmos.DrawLine(new Vector3(FinalNodeList[i].x, 0.1f, FinalNodeList[i].z), new Vector3(FinalNodeList[i + 1].x, 0.1f, FinalNodeList[i + 1].z));
            }
        }
    }
}
