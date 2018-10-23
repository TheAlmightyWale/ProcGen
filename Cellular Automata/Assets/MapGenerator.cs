using System;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{

    public int width;
    public int height;

    public string seed;
    public bool useRandomSeed;

    public float squareSize = 1.0f;
    public int borderSize = 5;
    public int passageSize = 2;
    public int WallRegionMinimumSize = 30;
    public int roomRegionMinimumSize = 30;

    public int smoothIterations = 5;
    public int smoothnessWallThreshold = 4;

    [Range(0, 100)]
    public int randomFillPercent;

    int[,] map;

    // Use this for initialization
    void Start()
    {
        GenerateMap();
    }

    void GenerateMap()
    {
        map = new int[width, height];
        RandomFillMap();

        for (int i = 0; i < smoothIterations; i++)
        {
            SmoothMap();
        }

        ProcessMap();

        //Create border
        int[,] borderedMap = new int[width + borderSize * 2, height + borderSize * 2];

        for (int x = 0; x < borderedMap.GetLength(0); x++)
        {
            for (int y = 0; y < borderedMap.GetLength(1); y++)
            {
                if (x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize)
                {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                }
                else
                {
                    borderedMap[x, y] = 1;
                }
            }
        }

        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(borderedMap, squareSize);
    }

    void ProcessMap()
    {
        List<List<Vector2Int>> wallRegions = GetRegions(1); // Wall regions type is 1

        foreach(List<Vector2Int> region in wallRegions)
        {
            if(region.Count < WallRegionMinimumSize)
            {
                foreach(Vector2Int tile in region)
                {
                    map[tile.x, tile.y] = 0; //TODO turn into a function, so different regions can be tagged and named 
                }
            }
        }

        List<List<Vector2Int>> roomRegions = GetRegions(0); // Room region type is 0
        List<Room> rooms = new List<Room>();

        foreach (List<Vector2Int> region in roomRegions)
        {
            if (region.Count < roomRegionMinimumSize)
            {
                foreach (Vector2Int tile in region)
                {
                    map[tile.x, tile.y] = 1; //TODO turn into a function, so different regions can be tagged and named 
                }
            }
            else
            {
                rooms.Add(new Room(region, map));
            }
        }

        rooms.Sort();
        rooms[0].isMainRoom = true;
        rooms[0].isAccessibleFromMainRoom = true;
        ConnectClosestRooms(rooms);
    }

    private void ConnectClosestRooms(List<Room> _rooms, bool _forceAccessbilityFromMainRoom = false)
    {
        List<Room> roomListA = new List<Room>();
        List<Room> roomListB = new List<Room>();

        if (_forceAccessbilityFromMainRoom)
        {
            foreach(Room r in _rooms)
            {
                if (r.isAccessibleFromMainRoom)
                {
                    roomListB.Add(r);
                }
                else
                {
                    roomListA.Add(r);
                }
            }
        }
        else
        {
            roomListA = _rooms;
            roomListB = _rooms;
        }

        int smallestDistance = 0;
        Vector2Int closestTileA = new Vector2Int();
        Vector2Int closestTileB = new Vector2Int();
        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false;

        foreach (Room roomA in roomListA)
        {
            if (!_forceAccessbilityFromMainRoom)
            {
                possibleConnectionFound = false;
                if(roomA.connectedRooms.Count > 0)
                {
                    continue;
                }
            }
            
            foreach (Room roomB in roomListB)
            {
                if(roomA == roomB || roomA.IsConnected(roomB))
                {
                    continue;
                }

                for( int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++)
                {
                    for(int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++)
                    {
                        Vector2Int tileA = roomA.edgeTiles[tileIndexA];
                        Vector2Int tileB = roomB.edgeTiles[tileIndexB];
                        int distanceBetweenRooms = (int)(Mathf.Pow(tileA.x - tileB.x, 2) + Mathf.Pow(tileA.y - tileB.y, 2));

                        if(distanceBetweenRooms < smallestDistance || !possibleConnectionFound)
                        {
                            smallestDistance = distanceBetweenRooms;
                            closestTileA = tileA;
                            closestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                            possibleConnectionFound = true;
                        }
                    }
                }
            }
            if (possibleConnectionFound && !_forceAccessbilityFromMainRoom)
            {
                CreatePassage(bestRoomA, bestRoomB, closestTileA, closestTileB);
            }
        }

        if (possibleConnectionFound && _forceAccessbilityFromMainRoom)
        {
            CreatePassage(bestRoomA, bestRoomB, closestTileA, closestTileB);
            ConnectClosestRooms(_rooms, true);
        }


        if (!_forceAccessbilityFromMainRoom)
        {
            ConnectClosestRooms(_rooms, true);
        }
    }

    void CreatePassage(Room _roomA, Room _roomB, Vector2Int _tileA, Vector2Int _tileB)
    {
        Room.ConnectRooms(_roomA, _roomB);
        Debug.DrawLine(TileToWorldPoint(_tileA), TileToWorldPoint(_tileB), Color.green, 100);

        List<Vector2Int> line = GetLine(_tileA, _tileB);
        foreach(Vector2Int point in line)
        {
            DrawCircle(point, passageSize);
        }
    }

    void DrawCircle(Vector2Int _point, int _radius)
    {
        for( int x = -_radius; x <= _radius; x++)
        {
            for (int y = -_radius; y <= _radius; y++)
            {
                if(x*x + y*y <= _radius * _radius)
                {
                    int drawX = _point.x + x;
                    int drawY = _point.y + y;
                    if(IsInMapRange(drawX, drawY))
                    {
                        map[drawX, drawY] = 0;
                    }
                }
            }
        }
    }

    List<Vector2Int> GetLine(Vector2Int _from, Vector2Int _to)
    {
        List<Vector2Int> line = new List<Vector2Int>();
        bool inverted = false;
        int dx = _to.x - _from.x;
        int dy = _to.y - _from.y;


        int step = Math.Sign(dx);
        int gradientStep = Math.Sign(dy);

        int longest = Math.Abs(dx);
        int shortest = Math.Abs(dy);

        if(longest < shortest)
        {
            inverted = true;
            int temp = shortest;
            shortest = longest;
            longest = temp;

            temp = step;
            step = gradientStep;
            gradientStep = temp;
        }

        int gradientAccumulation = longest / 2;

        int x = _from.x;
        int y = _from.y;

        for(int i = 0; i < longest; i++)
        {
            line.Add(new Vector2Int(x,y));

            if (inverted)
            {
                y += step;
            }
            else
            {
                x += step;
            }

            gradientAccumulation += shortest;
            if(gradientAccumulation >= longest)
            {
                if (inverted)
                {
                    x += gradientStep;
                }
                else
                {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }

        return line;
    }

    Vector3 TileToWorldPoint(Vector2Int _tile)
    {
        return new Vector3(-width/2 + 0.5f + _tile.x, 2, -height/2 + 0.5f + _tile.y);
    }

    List<List<Vector2Int>> GetRegions(int _tileType)
    {
        List<List<Vector2Int>> regions = new List<List<Vector2Int>>();
        int[,] mapFlags = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if(mapFlags[x,y] == 0 && map[x,y] == _tileType)
                {
                    List<Vector2Int> newRegion = GetRegionTiles(x,y);
                    regions.Add(newRegion);

                    //All tiles in region looked at
                    foreach( Vector2Int tile in newRegion)
                    {
                        mapFlags[tile.x, tile.y] = 1;
                    }
                }
            }
        }

        return regions;
    }

    List<Vector2Int> GetRegionTiles(int _startX, int _startY)
    {
        List<Vector2Int> tiles = new List<Vector2Int>();
        int[,] mapFlags = new int[width, height];
        int tileType = map[_startX, _startY];

        //Breadth first search
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(_startX, _startY));
        mapFlags[_startX, _startY] = 1; //Tile has been looked at

        while (queue.Count > 0)
        {
            Vector2Int tile = queue.Dequeue();
            tiles.Add(tile);

            for (int x = tile.x - 1; x <= tile.x + 1; x++)
            {
                for (int y = tile.y - 1; y <= tile.y + 1; y++)
                {
                    if (IsInMapRange(x, y) && (y == tile.y || x == tile.x))
                    {
                        if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                        {
                            mapFlags[x, y] = 1;
                            queue.Enqueue(new Vector2Int(x, y));
                        }
                    }
                }
            }
        }

        return tiles;
    }

    bool IsInMapRange(int _x, int _y)
    {
        return (_x >= 0 && _x < width && _y >= 0 && _y < height);
    }

    void RandomFillMap()
    {
        if (useRandomSeed)
        {
            seed = Time.time.ToString();
        }

        System.Random psuedoRandom = new System.Random(seed.GetHashCode());

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if ((x == 0 || x == width - 1 || y == 0 || y == height - 1))
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (psuedoRandom.Next(0, 100) < randomFillPercent) ? 1 : 0;
                }
            }
        }
    }

    void SmoothMap()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(x, y);

                if (neighbourWallTiles > smoothnessWallThreshold)
                {
                    map[x, y] = 1;
                }
                else if (neighbourWallTiles > smoothnessWallThreshold)
                {
                    map[x, y] = 0;
                }
            }
        }
    }

    int GetSurroundingWallCount(int _x, int _y)
    {
        int wallCount = 0;

        //Loop through surrounding grid squares
        for (int neighbourX = _x - 1; neighbourX <= _x + 1; neighbourX++)
        {
            for (int neighbourY = _y - 1; neighbourY <= _y + 1; neighbourY++)
            {
                //Check constraints
                if (IsInMapRange(neighbourX, neighbourY))
                {
                    //Ignore original tile
                    if (neighbourX != _x || neighbourY != _y)
                    {
                        wallCount += map[neighbourX, neighbourY];
                    }
                }
                else
                {
                    //Encourage growth of walls around edge of map
                    wallCount++;
                }

            }
        }

        return wallCount;
    }

    class Room : IComparable<Room>
    {
        public List<Vector2Int> tiles;
        public List<Vector2Int> edgeTiles;
        public List<Room> connectedRooms;
        public int roomSize;
        public bool isAccessibleFromMainRoom;
        public bool isMainRoom;

        public Room()
        {
        }

        public Room(List<Vector2Int> _tiles, int[,] _map)
        {
            tiles = _tiles;
            roomSize = tiles.Count;
            connectedRooms = new List<Room>();

            edgeTiles = new List<Vector2Int>();
            foreach(Vector2Int tile in tiles)
            {
                for(int x = tile.x -1; x <= tile.x + 1; x++)
                {
                    for (int y = tile.y - 1; y <= tile.y + 1; y++)
                    {
                        if(x == tile.x || y == tile.y)
                        {
                            if(_map[x,y] == 1)
                            {
                                edgeTiles.Add(tile);
                            }
                        }
                    }
                }
            }
        }

        public void SetAccessibleFromMainRoom()
        {
            if (!isAccessibleFromMainRoom)
            {
                isAccessibleFromMainRoom = true;
                foreach (Room r in connectedRooms)
                {
                    r.SetAccessibleFromMainRoom();
                }
            }
        }

        public static void ConnectRooms(Room _a, Room _b)
        {
            if (_a.isAccessibleFromMainRoom)
            {
                _b.SetAccessibleFromMainRoom();
            } else if (_b.isAccessibleFromMainRoom)
            {
                _a.SetAccessibleFromMainRoom();
            }

            _a.connectedRooms.Add(_b);
            _b.connectedRooms.Add(_a);
        }

        public bool IsConnected(Room _other)
        {
            return connectedRooms.Contains(_other);
        }

        public int CompareTo(Room _otherRoom)
        {
            return _otherRoom.roomSize.CompareTo(roomSize);
        }

    }
}
