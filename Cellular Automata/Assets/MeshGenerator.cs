using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour {

    public float wallHeight = 5.0f;
    public int textureTileAmount = 10;
    public SquareGrid squareGrid;
    public MeshFilter walls;
    public MeshFilter cave;

    List<Vector3> vertices;
    List<int> triangles;

    Dictionary<int, List<Triangle>> triangleDict = new Dictionary<int, List<Triangle>>();
    List<List<int>> outlines = new List<List<int>>();
    HashSet<int> checkedVertices = new HashSet<int>();

    public void GenerateMesh(int[,] _map, float _squareSize)
    {
        outlines.Clear();
        checkedVertices.Clear();
        triangleDict.Clear();

        squareGrid = new SquareGrid(_map, _squareSize);

        vertices = new List<Vector3>();
        triangles = new List<int>();

        for (int x = 0; x < squareGrid.grid.GetLength(0); x++)
        {
            for (int y = 0; y < squareGrid.grid.GetLength(1); y++)
            {
                TrigangulateSquare(squareGrid.grid[x, y]);
            }
        }

        Mesh mesh = new Mesh();
        cave.mesh = mesh;

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        Vector2[] uvs = new Vector2[vertices.Count];
        for(int i = 0; i < vertices.Count; i++)
        {
            float percentX = Mathf.InverseLerp(-_map.GetLength(0) / 2 * _squareSize, _map.GetLength(0) / 2 * _squareSize, vertices[i].x) * textureTileAmount;
            float percentY = Mathf.InverseLerp(-_map.GetLength(0) / 2 * _squareSize, _map.GetLength(0) / 2 * _squareSize, vertices[i].z) * textureTileAmount;

            uvs[i] = new Vector2(percentX, percentY);
        }
        mesh.uv = uvs;

        CreateWallMesh();
    }

    private void CreateWallMesh()
    {
        CalculateMeshOutlines();

        List<Vector3> wallVertices = new List<Vector3>();
        List<int> wallTriangles = new List<int>();
        Mesh wallMesh = new Mesh();

        foreach(List<int> outline in outlines)
        {
            for(int i = 0; i < outline.Count - 1; i++)
            {
                int startIndex = wallVertices.Count;
                wallVertices.Add(vertices[outline[i]]); // Left vertex
                wallVertices.Add(vertices[outline[i + 1]]); // Right vertex
                wallVertices.Add(vertices[outline[i]] - Vector3.up * wallHeight); // Bottom Left vertex
                wallVertices.Add(vertices[outline[i+1]] - Vector3.up * wallHeight); // Bottom right vertex

                //Assign indices - counter clockwise 
                wallTriangles.Add(startIndex + 0);
                wallTriangles.Add(startIndex + 2);
                wallTriangles.Add(startIndex + 3);

                wallTriangles.Add(startIndex + 3);
                wallTriangles.Add(startIndex + 1);
                wallTriangles.Add(startIndex + 0);
            }
        }
        wallMesh.vertices = wallVertices.ToArray();
        wallMesh.triangles = wallTriangles.ToArray();
        walls.mesh = wallMesh;

        MeshCollider wallCollider = walls.gameObject.AddComponent<MeshCollider>();
        wallCollider.sharedMesh = wallMesh;
    }

    public void TrigangulateSquare(Square _square)
    {
        switch (_square.configuration)
        {
            case 0:
                break;

            // 1 points:
            case 1:
                MeshFromPoints(_square.centerLeft, _square.centerBottom, _square.bottomLeft);
                break;
            case 2:
                MeshFromPoints(_square.bottomRight, _square.centerBottom, _square.centerRight);
                break;
            case 4:
                MeshFromPoints(_square.topRight, _square.centerRight, _square.centerTop);
                break;
            case 8:
                MeshFromPoints(_square.topLeft, _square.centerTop, _square.centerLeft);
                break;

            // 2 points:
            case 3:
                MeshFromPoints(_square.centerRight, _square.bottomRight, _square.bottomLeft, _square.centerLeft);
                break;
            case 6:
                MeshFromPoints(_square.centerTop, _square.topRight, _square.bottomRight, _square.centerBottom);
                break;
            case 9:
                MeshFromPoints(_square.topLeft, _square.centerTop, _square.centerBottom, _square.bottomLeft);
                break;
            case 12:
                MeshFromPoints(_square.topLeft, _square.topRight, _square.centerRight, _square.centerLeft);
                break;
            case 5:
                MeshFromPoints(_square.centerTop, _square.topRight, _square.centerRight, _square.centerBottom, _square.bottomLeft, _square.centerLeft);
                break;
            case 10:
                MeshFromPoints(_square.topLeft, _square.centerTop, _square.centerRight, _square.bottomRight, _square.centerBottom, _square.centerLeft);
                break;

            // 3 point:
            case 7:
                MeshFromPoints(_square.centerTop, _square.topRight, _square.bottomRight, _square.bottomLeft, _square.centerLeft);
                break;
            case 11:
                MeshFromPoints(_square.topLeft, _square.centerTop, _square.centerRight, _square.bottomRight, _square.bottomLeft);
                break;
            case 13:
                MeshFromPoints(_square.topLeft, _square.topRight, _square.centerRight, _square.centerBottom, _square.bottomLeft);
                break;
            case 14:
                MeshFromPoints(_square.topLeft, _square.topRight, _square.bottomRight, _square.centerBottom, _square.centerLeft);
                break;

            // 4 point:
            case 15:
                MeshFromPoints(_square.topLeft, _square.topRight, _square.bottomRight, _square.bottomLeft);
                //All surrounding nodes are walls, so will never be an outline
                checkedVertices.Add(_square.topLeft.vertexIndex);
                checkedVertices.Add(_square.topRight.vertexIndex);
                checkedVertices.Add(_square.bottomLeft.vertexIndex);
                checkedVertices.Add(_square.bottomRight.vertexIndex);
                break;
        }
    }

    private void MeshFromPoints(params Node[] _points)
    {
        AssignVertices(_points);

        if(_points.Length >= 3)
        {
            CreateTriangle(_points[0], _points[1], _points[2]);
        }

        if(_points.Length >= 4)
        {
            CreateTriangle(_points[0], _points[2], _points[3]);
        }

        if(_points.Length >= 5)
        {
            CreateTriangle(_points[0], _points[3], _points[4]);
        }

        if(_points.Length >= 6)
        {
            CreateTriangle(_points[0], _points[4], _points[5]);
        }
    }

    private void AssignVertices(Node[] _points)
    {
        for(int i = 0; i < _points.Length; i++)
        {
            //Not yet assigned point
            if(_points[i].vertexIndex == -1)
            {
                _points[i].vertexIndex = vertices.Count;
                vertices.Add(_points[i].position);
            }
        }
    }

    private void CreateTriangle(Node _a, Node _b, Node _c)
    {
        triangles.Add(_a.vertexIndex);
        triangles.Add(_b.vertexIndex);
        triangles.Add(_c.vertexIndex);

        Triangle triangle = new Triangle(_a.vertexIndex, _b.vertexIndex, _c.vertexIndex);
        AddTriangleToDictionary(triangle.vertexIndexA, triangle);
        AddTriangleToDictionary(triangle.vertexIndexB, triangle);
        AddTriangleToDictionary(triangle.vertexIndexC, triangle);

    }

    //Go through every vertex in the map, if it is an outline vertex follow outline and add outline to list
    void CalculateMeshOutlines()
    {
        for(int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
        {
            if (!checkedVertices.Contains(vertexIndex))
            {
                int newOutlineVertex = GetConnectedOutlineVertex(vertexIndex);
                if(newOutlineVertex != -1)
                {
                    checkedVertices.Add(vertexIndex);

                    List<int> newOutline = new List<int>();
                    newOutline.Add(vertexIndex);
                    outlines.Add(newOutline);
                    FollowOutline(newOutlineVertex, outlines.Count - 1);
                    outlines[outlines.Count - 1].Add(vertexIndex);
                }
            }
        }
    }

    //Recursively follow outline
    private void FollowOutline(int _vertexIndex, int _outlineIndex)
    {
        outlines[_outlineIndex].Add(_vertexIndex);
        checkedVertices.Add(_vertexIndex);
        int nextVertexIndex = GetConnectedOutlineVertex(_vertexIndex);

        if(nextVertexIndex != -1)
        {
            FollowOutline(nextVertexIndex, _outlineIndex);
        }
    }

    int GetConnectedOutlineVertex(int _vertexIndex)
    {
        List<Triangle> vertexTriangles = triangleDict[_vertexIndex];

        for(int i = 0; i < vertexTriangles.Count; i++)
        {
            Triangle triangle = vertexTriangles[i];
            

            for(int j = 0; j < 3; j++) //3 indices in a triangle
            {
                int vertexB = triangle[j];
                if (vertexB != _vertexIndex && !checkedVertices.Contains(vertexB))
                {

                    if (IsOutlineEdge(_vertexIndex, vertexB))
                    {
                        return vertexB;
                    }
                }
            }

        }

        //No Connected vertices
        return -1;
    }

    private bool IsOutlineEdge(int _vertexA, int _vertexB)
    {
        List<Triangle> vertexATriangles = triangleDict[_vertexA];
        int sharedTriangleCount = 0;

        for(int i = 0; i < vertexATriangles.Count; i++)
        {
            if (vertexATriangles[i].Contains(_vertexB))
            {
                sharedTriangleCount++;
                if(sharedTriangleCount > 1)
                {
                    break;
                }
            }
        }
        return sharedTriangleCount == 1;
    }

    private void AddTriangleToDictionary(int _vertexIndexKey, Triangle _triangle)
    {
        if (triangleDict.ContainsKey(_vertexIndexKey))
        {
            triangleDict[_vertexIndexKey].Add(_triangle);
        }
        else
        {
            List<Triangle> triangleList = new List<Triangle>();
            triangleList.Add(_triangle);
            triangleDict.Add(_vertexIndexKey, triangleList);
        }
    }

    struct Triangle
    {
        public int vertexIndexA;
        public int vertexIndexB;
        public int vertexIndexC;
        int[] vertices;

        public Triangle(int _a, int _b, int _c)
        {
            vertexIndexA = _a;
            vertexIndexB = _b;
            vertexIndexC = _c;

            vertices = new int[3];
            vertices[0] = _a;
            vertices[1] = _b;
            vertices[2] = _c;
        }

        public int this[int i]
        {
            get
            {
                return vertices[i];
            }
        }

        public bool Contains(int _vertexIndex)
        {
            return _vertexIndex == vertexIndexA || _vertexIndex == vertexIndexB || _vertexIndex == vertexIndexC;
        }
    }

    public class SquareGrid
    {
        public Square[,] grid;

        public SquareGrid(int[,] _map, float _squareSize)
        {
            int nodeCountX = _map.GetLength(0);
            int nodeCountY = _map.GetLength(1);
            float mapWidth = _squareSize * nodeCountX;
            float mapHeight = _squareSize * nodeCountY;

            ControlNode[,] controlNodes = new ControlNode[nodeCountX, nodeCountY];

            for(int x = 0; x < nodeCountX; x++)
            {
                for(int y = 0; y < nodeCountY; y++)
                {
                    Vector3 pos = new Vector3(-mapWidth / 2 + x * _squareSize + _squareSize / 2, 0, -mapHeight / 2 + y * _squareSize + _squareSize / 2);
                    controlNodes[x, y] = new ControlNode(pos, _map[x, y] == 1, _squareSize);
                }
            }

            grid = new Square[nodeCountX - 1, nodeCountY - 1];
            for (int x = 0; x < nodeCountX - 1; x++)
            {
                for (int y = 0; y < nodeCountY - 1; y++)
                {
                    grid[x, y] = new Square(controlNodes[x, y + 1], controlNodes[x + 1, y + 1], controlNodes[x + 1, y], controlNodes[x, y]);
                }
            }
        }
    }

    public class Square
    {
        public int configuration = 0;
        public ControlNode topLeft, topRight, bottomLeft, bottomRight;
        public Node centerTop, centerRight, centerBottom, centerLeft;

        public Square(ControlNode _topLeft, ControlNode _topRight, ControlNode _bottomRight, ControlNode _bottomLeft)
        {
            topLeft = _topLeft;
            topRight = _topRight;
            bottomLeft = _bottomLeft;
            bottomRight = _bottomRight;

            centerTop = topLeft.right;
            centerBottom = bottomLeft.right;
            centerRight = bottomRight.above;
            centerLeft = bottomLeft.above;

            if (topLeft.active)
            {
                configuration += 8;
            }

            if (topRight.active)
            {
                configuration += 4;
            }

            if (bottomRight.active)
            {
                configuration += 2;
            }

            if (bottomLeft.active)
            {
                configuration += 1;
            }
        }
    }


	public class Node
    {
        public Vector3 position;
        public int vertexIndex = -1;

        public Node(Vector3 _pos)
        {
            position = _pos;
        }
    }

    public class ControlNode : Node
    {
        public bool active;
        public Node above, right;

        public ControlNode(Vector3 _pos, bool _active, float _squareSize) : base(_pos)
        {
            active = _active;
            above = new Node(position + Vector3.forward * (_squareSize / 2.0f));
            right = new Node(position + Vector3.right * (_squareSize / 2.0f));
        }
    }
}
