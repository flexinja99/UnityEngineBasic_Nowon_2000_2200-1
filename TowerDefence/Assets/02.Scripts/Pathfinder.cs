using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class Pathfinder : MonoBehaviour
{
    struct Coord
    {
        public static Coord zero = new Coord(0, 0);
        public static Coord error = new Coord(-1, -1);
        public int y;
        public int x;

        public Coord(int y, int x)
        {
            this.y = y;
            this.x = x;
        }

        public static bool operator ==(Coord op1, Coord op2)
            => (op1.y == op2.y && op1.x == op2.x);

        public static bool operator !=(Coord op1, Coord op2)
            => !(op1 == op2);
    }

    enum NodeType
    {
        None,
        Path,
        Obstacle
    }

    public enum FindingOptions
    {
        BFS,
        DFS
    }

    struct NodePair
    {
        public Transform node;
        public Coord coord;
        public NodeType type;
    }

    private Transform _leftBottom; // (0, 0);
    private Transform _rightTop; // (max, max);
    private float _width => _rightTop.position.x - _leftBottom.position.x; // ���� �� �ʺ�
    private float _height => _rightTop.position.z - _leftBottom.position.z; // ���� �� ����
    private float _nodeTerm = 1.0f; // ��� ���� ����
    private NodePair[,] _map;
    private bool[,] _visited;
    private int[,] _direction = new int[2, 4]
    {
        {-1, 0, 1, 0 },
        { 0,-1, 0, 1 }
    };
    private List<List<Coord>> _pathList = new List<List<Coord>>();
    private List<Coord> _tmpPathForDFS = new List<Coord>();

    public void SetNodeMap(List<Transform> pathNodes, List<Transform> obstacleNodes)
    {
        // ��� ��� ����
        List<Transform> nodes = new List<Transform>();
        nodes.AddRange(pathNodes);
        nodes.AddRange(obstacleNodes);
        IOrderedEnumerable<Transform> nodesFiltered = nodes.OrderBy(node => node.position.x + node.position.z);

        // ���ʾƷ���, ���������� ��� ã��
        _leftBottom = nodesFiltered.First();
        _rightTop = nodesFiltered.Last();


        _map = new NodePair[(int)(_height / _nodeTerm) + 1, (int)(_width / _nodeTerm) + 1];
        _visited = new bool[_map.GetLength(0), _map.GetLength(1)];

        Coord tmpCoord;
        foreach (var node in pathNodes)
        {
            tmpCoord = TransformToCoord(node);
            _map[tmpCoord.y, tmpCoord.x] = new NodePair()
            {
                node = node,
                coord = tmpCoord,
                type = NodeType.Path
            };
        }

        foreach (var node in obstacleNodes)
        {
            tmpCoord = TransformToCoord(node);
            _map[tmpCoord.y, tmpCoord.x] = new NodePair()
            {
                node = node,
                coord = tmpCoord,
                type = NodeType.Obstacle
            };
        }
    }

    public void FindOptimizedPath(Transform startNode, Transform endNode, FindingOptions option)
    {
        bool found = false;

        switch (option)
        {
            case FindingOptions.BFS:
                found = BFS(start: FindNode(startNode).coord,
                            end  : FindNode(endNode).coord);
                break;
            case FindingOptions.DFS:
                found = DFS(start: FindNode(startNode).coord,
                            end  : FindNode(endNode).coord);
                break;
            default:
                break;
        }

        

        if (found)
        {
            List<List<Transform>> nodePathList = new List<List<Transform>>();

            for (int i = 0; i < _pathList.Count; i++)
            {
                string pathString = "";
                nodePathList.Add(new List<Transform>());
                for (int j = 0; j < _pathList[i].Count; j++)
                {
                    nodePathList[i].Add(GetNode(_pathList[i][j]));
                    pathString += $"-> {nodePathList[i][j].name}";
                }
                Debug.Log($"[Pathfinder] : �ִܰ�� Ž�� ��. {pathString}");
            }
        }
    }

    private bool BFS(Coord start, Coord end)
    {
        bool isFinished = false;
        List<KeyValuePair<Coord,Coord>> parentPairs = new List<KeyValuePair<Coord, Coord>>();
        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(start);
        parentPairs.Add(new KeyValuePair<Coord, Coord>(Coord.error, start));
        _visited[start.y, start.x] = true;

        int searchCount = 0;
        while (queue.Count > 0)
        {
            Coord parent = queue.Dequeue();

            for (int i = 0; i < _direction.GetLength(1); i++)
            {
                Coord next = new Coord(parent.y + _direction[0, i], parent.x + _direction[1, i]);

                // Ž�� ��ġ�� ���� �������
                if (next.y < 0 || next.y >= _map.GetLength(0) ||
                    next.x < 0 || next.x >= _map.GetLength(1))
                    continue;

                // Ž�� ��ġ�� ��ֹ��� ���
                if (_map[next.y, next.x].type == NodeType.Obstacle)
                    continue;

                // �湮 ����
                if (_visited[next.y, next.x] == true)
                    continue;

                // �湮
                searchCount++;
                parentPairs.Add(new KeyValuePair<Coord, Coord>(parent, next));
                _visited[next.y, next.x] = true;

                // ���� üũ
                if (next.y == end.y &&
                    next.x == end.x)
                {
                    isFinished = true;
                    _pathList.Add(CalcPath(parentPairs, start, end));
                }
                else
                {
                    queue.Enqueue(next);
                }
            }
        }

        return isFinished;
    }

    private bool DFS(Coord start, Coord end)
    {
        _visited[start.y, start.x] = true;
        _tmpPathForDFS.Add(start);
        Coord next;
        for (int i = 0; i < _direction.GetLength(1); i++)
        {
            next.y = start.y + _direction[0, i];
            next.x = start.x + _direction[1, i];

            // Ž�� ��ġ�� ���� �������
            if ((next.y < 0 || next.y >= _map.GetLength(0))||
                (next.x < 0 || next.x >= _map.GetLength(1)))
                continue;

            // Ž�� ��ġ�� ��ֹ��� ���
            if (_map[next.y, next.x].type == NodeType.Obstacle)
                continue;

            // �湮 ����
            if (_visited[next.y, next.x] == true)
                continue;

            // ���� ����
            if (next.y == end.y &&
                next.x == end.x)
            {
                _pathList.Add(_tmpPathForDFS);
                return true;
            }
            else
            {
                return DFS(next, end);
            }
        }

        return false;
    }


    /// <summary>
    /// ��ϵ� �θ�/�ڽ� ��� ����� ����Ʈ�� 
    /// ��θ� ����ؼ� ��ȯ���ִ� �Լ�
    /// </summary>
    /// <returns>���� ���</returns>
    private List<Coord> CalcPath(List<KeyValuePair<Coord, Coord>> parentPairs, Coord start, Coord end)
    {
        List<Coord> path = new List<Coord>();

        Coord coord = parentPairs.Last().Value; // �� ������ ���
        path.Add(coord);

        int index = parentPairs.Count - 1;
        while (index > 0 &&
               parentPairs[index].Key != start)
        {
            path.Add(parentPairs[index].Key);
            index = parentPairs.FindLastIndex(pair => pair.Value == parentPairs[index].Key);
        }
        path.Add(start);

        path.Reverse();

        return path;
    }


    private Coord TransformToCoord(Transform node)
    {
        return new Coord((int)((node.position.z - _leftBottom.position.z) / _nodeTerm),
                         (int)((node.position.x - _leftBottom.position.x) / _nodeTerm));
    }

    private NodePair FindNode(Transform node)
    {
        Coord coord = TransformToCoord(node);
        for (int i = 0; i < _map.GetLength(1); i++)
            for (int j = 0; j < _map.GetLength(0); j++)
                if (_map[j,i].coord == coord)
                    return _map[j,i];

        return new NodePair() { coord = Coord.error, node = null, type = NodeType.None };
    }

    private Transform GetNode(Coord coord)
    {
        return _map[coord.y, coord.x].node;
    }

    private void Start()
    {
        Transform nodeParent = GameObject.Find("Nodes").transform;
        Transform[] nodes = new Transform[nodeParent.childCount];
        for (int i = 0; i < nodeParent.childCount; i++)
            nodes[i] = nodeParent.GetChild(i);

        Transform enemyPathParent = GameObject.Find("EnemyPaths").transform;
        Transform[] enemyPaths = new Transform[enemyPathParent.childCount];
        for (int i = 0; i < enemyPathParent.childCount; i++)
            enemyPaths[i] = enemyPathParent.GetChild(i);

        SetNodeMap(enemyPaths.ToList(), nodes.ToList());
    }
}
