using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;

/*

TODO:

-- optimize placing obstacles (current implementation keeps randomly placing until no overlap -> inefficient for large num of obstacles in chunk)
 -> kinda done: will try to place 3 times before giving up

-- make trees spawn in groups (sometimes?)

-- add biomes etc.

-- option to show tempOffsets/tempLevels as colored chunks

-- implement neighours for chunks
 -> done?: loaded chunks see neighbouring loaded chunks as neighbours

*/

public class Main : MonoBehaviour
{
    GameObject g_map;
    public Tilemap map;
    public Tile[,] tiles;
    public Quadtree topTree;

    public static int chunkSize = 20; //length of one side -> should be even
    public static int quadtreeMaxLevel = 1;
    public static int quadtreeSideLength = chunkSize * (int)Mathf.Pow(2,quadtreeMaxLevel-1);

    public GameObject player;
    public int playerSpeed;
    Vector3 nextMove;
    Quadtree lastTree;

    public GameObject[] obstacles;
    public int obstaclesPerTile;

    public static int time;

    public static GameObject[] debugChunkSquares;

    public static bool debug_drawTrees = false;
    public static bool debug_drawTempColors = false;
    public static bool debug_drawTempOffsets = false;


    // Start is called before the first frame update
    void Start()
    {
        g_map = new GameObject("map");
        map = g_map.AddComponent<Tilemap>() as Tilemap;
        Grid grid = g_map.AddComponent<Grid>() as Grid;
        TilemapRenderer tr = g_map.AddComponent<TilemapRenderer>() as TilemapRenderer;
        tr.sortingLayerID = SortingLayer.NameToID("mapLayer");
        tiles = new Tile[Chunk.numTempLevels+1,5];
        initTiles();
        initTrees();
        initEntities();
        nextMove = new Vector3(0,0,0);
        playerSpeed = 10;

        obstaclesPerTile = 10;

        time = 0;

        Camera.main.orthographicSize = 10;
        
        loadChunksAtPlayerPos();
        removeObstaclesAtPlayerPos();
        lastTree = getTreeFromPos(player.transform.position);
        
        initDebugChunkSquares();
        if (debug_drawTrees) drawAllTrees();
        if (debug_drawTempColors) drawTempColors();
        if (debug_drawTempOffsets) drawTempOffsets();
        //Debug.DrawLine(new Vector3(0,0,0), new Vector3(1,1,0), Color.red, 100000000, false);

        //TileBase[] allTiles = map.GetTilesBlock(map.cellBounds);
    }

    // Update is called once per frame
    void Update()
    {
        resetMove();
        if (Input.GetMouseButtonDown(0)) {
            addRandomGrassAtBlock(-chunkSize/2,-chunkSize/2,chunkSize/2,chunkSize/2);
        }
        if (Input.GetKey("w") || Input.GetKey("up")) {
            nextMove.y = 1;
        }
        if (Input.GetKey("a") || Input.GetKey("left")) {
            nextMove.x = -1;
            player.transform.localScale = new Vector3(1,1,1);
        }
        if (Input.GetKey("s") || Input.GetKey("down")) {
            nextMove.y = -1;
        }
        if (Input.GetKey("d") || Input.GetKey("right")) {
            nextMove.x = 1;
            player.transform.localScale = new Vector3(-1,1,1);
        }
        move();
    }

    void FixedUpdate()
    {
        time++;
        
        if (time % 10 == 0) {
            bool newChunk = lastTree != getTreeFromPos(player.transform.position);
            if (newChunk) {
                loadChunksAtPlayerPos();
                unloadChunksAroundPlayerPos();
                if (debug_drawTrees) drawAllTrees();
                if (debug_drawTempColors) drawTempColors();
                if (debug_drawTempOffsets) drawTempOffsets();
            }
            if (newChunk) lastTree = getTreeFromPos(player.transform.position);
        }
        
        updateSortingOrder();
        //if (time % 100 == 0) 
    }

    void updateSortingOrder() {
        SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
        sr.sortingOrder = Mathf.RoundToInt(player.transform.position.y) * -1;
    }

    void initEntities() {
        initPlayer();
        initObstacles();
    }

    void initObstacles() {
        obstacles = new GameObject[2];
        initTreeObs();
        initStoneObs();
    }

    void initTreeObs() {
        byte[] b_tree = File.ReadAllBytes("Assets/Entities/tree1.png");
        Texture2D t_tree = new Texture2D(10,10);
        t_tree.LoadImage(b_tree);
        t_tree.filterMode = FilterMode.Point;
        Sprite s_tree = Sprite.Create(t_tree, new Rect(0,0,t_tree.width,t_tree.height),new Vector2(0f, 0f),10);
        GameObject firstTree = new GameObject("tree");
        firstTree.layer = 10;
        firstTree.SetActive(false);
        SpriteRenderer sr = firstTree.AddComponent<SpriteRenderer>() as SpriteRenderer;
        sr.sprite = s_tree;
        BoxCollider2D collider = firstTree.AddComponent<BoxCollider2D>() as BoxCollider2D;
        collider.size = new Vector2(1,1);
        obstacles[0] = firstTree;
    }

    void initStoneObs() {
        byte[] b_stone = File.ReadAllBytes("Assets/Entities/stone1.png");
        Texture2D t_stone = new Texture2D(10,10);
        t_stone.LoadImage(b_stone);
        t_stone.wrapMode = TextureWrapMode.Clamp;
        t_stone.filterMode = FilterMode.Point;
        Sprite s_tree = Sprite.Create(t_stone, new Rect(0,0,t_stone.width,t_stone.height),new Vector2(0f, 0f),10);
        GameObject firstStone = new GameObject("stone");
        firstStone.layer = 10;
        firstStone.SetActive(false);
        SpriteRenderer sr = firstStone.AddComponent<SpriteRenderer>() as SpriteRenderer;
        sr.sprite = s_tree;
        BoxCollider2D collider = firstStone.AddComponent<BoxCollider2D>() as BoxCollider2D;
        collider.size = new Vector2(1,1);
        obstacles[1] = firstStone;
    }

    void move() {
        movePlayer();
    }

    void movePlayer() {
        Vector3 m = Vector3.Normalize(nextMove)*playerSpeed*Time.deltaTime;
        Vector3 mX = new Vector3(m.x,0,0);
        Vector3 mY = new Vector3(0,m.y,0);
        RaycastHit2D hit = Physics2D.Raycast(player.transform.position, m, 0.5f, LayerMask.GetMask("obstacles"));
        if (hit.collider == null) {
            player.transform.Translate(m);
            Camera.main.transform.position = player.transform.position - new Vector3(0,0,10);
        } else if (Physics2D.Raycast(player.transform.position, mX, 0.5f, LayerMask.GetMask("obstacles")).collider == null) {
            player.transform.Translate(mX);
            Camera.main.transform.position = player.transform.position - new Vector3(0,0,10);
        } else if (Physics2D.Raycast(player.transform.position, mY, 0.5f, LayerMask.GetMask("obstacles")).collider == null) {
            player.transform.Translate(mY);
            Camera.main.transform.position = player.transform.position - new Vector3(0,0,10);
        }
    }

    void resetMove() {
        nextMove.x = 0;
        nextMove.y = 0;
    }

    void initTrees() {
        BoundsInt bi = new BoundsInt();
        bi.SetMinMax(new Vector3Int(-chunkSize/2,-chunkSize/2,0), new Vector3Int(chunkSize/2,chunkSize/2,1));
        topTree = new Chunk(null,bi);
        ((Chunk)topTree).tempLevel = 0;
        ((Chunk)topTree).hasTemp = true;
    }

    void loadChunksAtPlayerPos() {
        Chunk cur = getTreeFromPos(player.transform.position);
        Chunk[] n = new Chunk[8];
        n[0] = getTreeFromPos(player.transform.position + new Vector3(-chunkSize,chunkSize,0));
        n[1] = getTreeFromPos(player.transform.position + new Vector3(0,chunkSize,0));
        n[2] = getTreeFromPos(player.transform.position + new Vector3(chunkSize,chunkSize,0));
        n[3] = getTreeFromPos(player.transform.position + new Vector3(chunkSize,0,0));
        n[4] = getTreeFromPos(player.transform.position + new Vector3(chunkSize,-chunkSize,0));
        n[5] = getTreeFromPos(player.transform.position + new Vector3(0,-chunkSize,0));
        n[6] = getTreeFromPos(player.transform.position + new Vector3(-chunkSize,-chunkSize,0));
        n[7] = getTreeFromPos(player.transform.position + new Vector3(-chunkSize,0,0));
        n[0].addNeighbour(n[1],3);
        n[0].addNeighbour(cur,4);
        n[0].addNeighbour(n[7],5);
        n[1].addNeighbour(n[2],3);
        n[1].addNeighbour(n[3],4);
        n[1].addNeighbour(cur,5);
        n[1].addNeighbour(n[7],6);
        n[1].addNeighbour(n[0],7);
        n[2].addNeighbour(n[3],5);
        n[2].addNeighbour(cur,6);
        n[2].addNeighbour(n[1],7);
        n[3].addNeighbour(n[4],5);
        n[3].addNeighbour(n[5],6);
        n[3].addNeighbour(cur,7);
        n[3].addNeighbour(n[1],0);
        n[3].addNeighbour(n[2],1);
        n[4].addNeighbour(n[5],7);
        n[4].addNeighbour(cur,0);
        n[4].addNeighbour(n[3],1);
        n[5].addNeighbour(n[6],7);
        n[5].addNeighbour(n[7],0);
        n[5].addNeighbour(cur,1);
        n[5].addNeighbour(n[3],2);
        n[5].addNeighbour(n[4],3);
        n[6].addNeighbour(n[7],1);
        n[6].addNeighbour(cur,2);
        n[6].addNeighbour(n[5],3);
        n[7].addNeighbour(n[0],1);
        n[7].addNeighbour(n[1],2);
        n[7].addNeighbour(cur,3);
        n[7].addNeighbour(n[5],4);
        n[7].addNeighbour(n[6],5);
        cur.neighbours = n;
        setTreeTempLevel(cur);
        loadChunkAtTree(cur);
        foreach (Chunk c in cur.neighbours) {
            setTreeTempLevel(c);
            loadChunkAtTree(c);
        }
    }

    void unloadChunksAroundPlayerPos() {
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(-chunkSize*2,chunkSize*2,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(-chunkSize,chunkSize*2,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(0,chunkSize*2,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(chunkSize,chunkSize*2,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(chunkSize*2,chunkSize*2,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(chunkSize*2,chunkSize,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(chunkSize*2,0,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(chunkSize*2,-chunkSize,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(chunkSize*2,-chunkSize*2,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(chunkSize,-chunkSize*2,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(0,-chunkSize*2,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(-chunkSize,-chunkSize*2,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(-chunkSize*2,-chunkSize*2,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(-chunkSize*2,-chunkSize,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(-chunkSize*2,0,0)));
        unloadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(-chunkSize*2,chunkSize,0)));
    }

    void unloadChunkAtTree(Chunk q) {
        if (q.getLoaded()) {
            q.unloadObstacles();
        }
    }

    void loadChunkAtTree(Chunk q) {
        if (!q.getLoaded()) {
            //addRandomGrassAtChunk(q.area);
            loadTilesAtChunk(getTilesForChunk(q),q);
            q.setLoaded(true);
            placeObstacles(q);
            q.setObstaclesLoaded(true);
        } else if (!q.getObstaclesLoaded()) {
            replaceObstacles(q);
            q.setObstaclesLoaded(true);
        }
    }

    void unloadObstaclesAtTree(Chunk q) {
        if (q.getObstaclesLoaded()) {
            q.unloadObstacles();
            q.setObstaclesLoaded(false);
        }
    }

    void placeObstacles(Chunk q) {
        BoundsInt area = q.area;
        List<Obstacle> os = new List<Obstacle>();
        for (int i = 0; i < obstaclesPerTile; i++) {
            placeObstacle(area,os,3);
        }
        q.setObstacles(os);
    }

    void placeObstacle(BoundsInt area, List<Obstacle> os, int tryNum) {
        if (tryNum == 0) return;
        Vector2Int pos = new Vector2Int(Random.Range(area.x,area.xMax),Random.Range(area.y,area.yMax));
        if (checkFree(pos, os)) {
            os.Add(spawnObstacleAt(pos,Random.Range(0,obstacles.Length)));
        } else {
            placeObstacle(area,os,tryNum-1);
        }
    }

    bool checkFree(Vector2Int pos, List<Obstacle> os) {
        foreach (Obstacle o in os) {
            if (o.pos == pos)  return false;
        }
        return true;
    }

    void replaceObstacles(Chunk q) {
        foreach (Obstacle o in q.getObstacles()) {
            o.gameObject.SetActive(true);
        }
    }

    Obstacle spawnObstacleAt(Vector2Int pos, int type) {
        GameObject newO = Object.Instantiate(obstacles[type]);
        newO.transform.position = new Vector3((float)pos.x,(float)pos.y,0.5f);
        newO.GetComponent<SpriteRenderer>().sortingOrder = (int)newO.transform.position.y * -1;
        newO.SetActive(true);
        return new Obstacle(pos,type,newO);
    }

    void removeObstaclesAtPlayerPos() {
        removeObstaclesAtPos(player.transform.position);
    }

    void removeObstaclesAtPos(Vector3 pos) {
        Chunk q = getTreeFromPos(pos);
        Vector2Int pos2 = new Vector2Int((int)pos.x,(int)pos.y);
        BoundsInt bi = new BoundsInt();
        bi.SetMinMax(new Vector3Int(pos2.x-1,pos2.y-1,0),new Vector3Int(pos2.x+1,pos2.y+1,1));
        List<Obstacle> os = q.getObstacles();
        for (int i = 0; i < os.Count; i++) {
            if (bi.Contains(new Vector3Int(os[i].pos.x,os[i].pos.y,0))) {
                Destroy(q.getObstacles()[i].gameObject);
                q.getObstacles().RemoveAt(i);
            }
        }
        q.setObstacles(os);
    }

    Chunk getTreeFromPos(Vector3 p) {
        Vector3Int pi = Vector3Int.FloorToInt(p);
        if (topTree.area.Contains(pi)) {
            return (Chunk)topTree.getTreeFromPos(pi);
        }
        extendTree(p);
        return (Chunk)topTree.getTreeFromPos(pi);
    }

    Chunk getTreeFromPos(Vector3Int p) {
        if (topTree.area.Contains(p)) {
            return (Chunk)topTree.getTreeFromPos(p);
        }
        extendTree(p);
        return (Chunk)topTree.getTreeFromPos(p);
    }

    Chunk getTreeFromPosIfExists(Vector3Int p) {
        if (topTree.area.Contains(p)) {
            return (Chunk)topTree.getTreeFromPosIfExists(p);
        }
        return null;
    }

    void extendTree(Vector3 p) {
        if (p.x < topTree.area.center.x) {
            if (p.y > topTree.area.center.y) {
                //bottom right of new
                Quadtree old = topTree;
                BoundsInt bi = new BoundsInt();
                BoundsInt obi = old.area;
                bi.SetMinMax(new Vector3Int(obi.x-obi.size.x,obi.y,0), new Vector3Int(obi.xMax,obi.yMax+obi.size.y,1));
                topTree = new Quadtree(null, bi);
                topTree.level = old.level + 1;
                topTree.split();
                topTree.children[2] = old;
                foreach (Quadtree q in topTree.children) {
                    q.parent = topTree;
                }
                if (old.level > 1) {
                    topTree.children[0].tempOffset = weightedRandOffset(old.tempOffset);
                    topTree.children[1].tempOffset = weightedRandOffset(old.tempOffset);
                    topTree.children[3].tempOffset = weightedRandOffset(old.tempOffset);
                    setTopTreeTempOffset();
                } else {
                    topTree.tempOffset = 0;
                }
            } else {
                //top right of new
                Quadtree old = topTree;
                BoundsInt bi = new BoundsInt();
                BoundsInt obi = old.area;
                bi.SetMinMax(new Vector3Int(obi.x-obi.size.x,obi.y-obi.size.y,0), obi.max);
                topTree = new Quadtree(null, bi);
                topTree.level = old.level + 1;
                topTree.split();
                topTree.children[1] = old;
                if (old.level > 1) {
                    topTree.children[0].tempOffset = weightedRandOffset(old.tempOffset);
                    topTree.children[2].tempOffset = weightedRandOffset(old.tempOffset);
                    topTree.children[3].tempOffset = weightedRandOffset(old.tempOffset);
                    setTopTreeTempOffset();
                } else {
                    topTree.tempOffset = 0;
                }
            }
        } else {
            if (p.y > topTree.area.center.y) {
                //bottom left of new
                Quadtree old = topTree;
                BoundsInt bi = new BoundsInt();
                BoundsInt obi = old.area;
                bi.SetMinMax(obi.min, new Vector3Int(obi.xMax+obi.size.x,obi.yMax+obi.size.y,1));
                topTree = new Quadtree(null, bi);
                topTree.level = old.level + 1;
                topTree.split();
                topTree.children[3] = old;
                if (old.level > 1) {
                    topTree.children[0].tempOffset = weightedRandOffset(old.tempOffset);
                    topTree.children[1].tempOffset = weightedRandOffset(old.tempOffset);
                    topTree.children[2].tempOffset = weightedRandOffset(old.tempOffset);
                    setTopTreeTempOffset();
                } else {
                    topTree.tempOffset = 0;
                }
            } else {
                //top left of new
                Quadtree old = topTree;
                BoundsInt bi = new BoundsInt();
                BoundsInt obi = old.area;
                bi.SetMinMax(new Vector3Int(obi.x,obi.y-obi.size.y,0), new Vector3Int(obi.xMax+obi.size.x,obi.yMax,1));
                topTree = new Quadtree(null, bi);
                topTree.level = old.level + 1;
                topTree.split();
                topTree.children[0] = old;
                if (old.level > 1) {
                    topTree.children[2].tempOffset = weightedRandOffset(old.tempOffset);
                    topTree.children[1].tempOffset = weightedRandOffset(old.tempOffset);
                    topTree.children[3].tempOffset = weightedRandOffset(old.tempOffset);
                    setTopTreeTempOffset();
                } else {
                    topTree.tempOffset = 0;
                }
            }
        }
    }

    void setTreeTempLevel(Chunk c) {
        updateNeighbours(c);
        if (c.hasTemp) return;
        List<int> temps = new List<int>();
        int num = 0;
        foreach (Chunk n in c.neighbours) {
            if (n != null && n.hasTemp && n.tempLevel != Chunk.magicBiomeTemp && !temps.Contains(n.tempLevel)) {
                temps.Add(n.tempLevel);
                num++;
            }
        }
        int offset = c.parent.tempOffset;
        if (Random.value < 0.5) {
            c.tempLevel = Chunk.magicBiomeTemp;
            c.hasTemp = true;
            return;
        }
        if (num == 0) {
            c.tempLevel = offset;
        } else if (num == 1) {
            c.tempLevel = temps[0] + offset;
            if (c.tempLevel > Chunk.maxTempLevel) {
                c.tempLevel = Chunk.maxTempLevel;
            } else if (c.tempLevel < Chunk.minTempLevel) {
                c.tempLevel = Chunk.minTempLevel;
            }
        } else if (num == 2) {
            int maxT = Mathf.Max(temps[0],temps[1]);
            int minT = Mathf.Min(temps[0],temps[1]);
            if (maxT - minT < 2) {
                if (offset == 1) {
                    c.tempLevel = maxT;
                } else if (offset == -1) {
                    c.tempLevel = minT;
                } else {
                    c.tempLevel = temps[Random.Range(0,2)];
                }
            } else {
                c.tempLevel = (maxT+minT)/2;
            }
        } else {
            int maxT = Mathf.Max(temps.ToArray());
            int minT = Mathf.Min(temps.ToArray());
            if (maxT - minT < 3) {
                c.tempLevel = (maxT+minT)/2;
            } else {
                c.tempLevel = Chunk.magicBiomeTemp;
            }
        }
        c.hasTemp = true;
    }

    void updateNeighbours(Chunk c) {
        Vector3Int center = new Vector3Int((int)c.area.center.x,(int)c.area.center.y,0);
        if (c.neighbours[0] == null) c.neighbours[0] = getTreeFromPosIfExists(center + new Vector3Int(-chunkSize,chunkSize,0));
        if (c.neighbours[1] == null) c.neighbours[1] = getTreeFromPosIfExists(center + new Vector3Int(0,chunkSize,0));
        if (c.neighbours[2] == null) c.neighbours[2] = getTreeFromPosIfExists(center + new Vector3Int(chunkSize,chunkSize,0));
        if (c.neighbours[3] == null) c.neighbours[3] = getTreeFromPosIfExists(center + new Vector3Int(chunkSize,0,0));
        if (c.neighbours[4] == null) c.neighbours[4] = getTreeFromPosIfExists(center + new Vector3Int(chunkSize,-chunkSize,0));
        if (c.neighbours[5] == null) c.neighbours[5] = getTreeFromPosIfExists(center + new Vector3Int(0,-chunkSize,0));
        if (c.neighbours[6] == null) c.neighbours[6] = getTreeFromPosIfExists(center + new Vector3Int(-chunkSize,-chunkSize,0));
        if (c.neighbours[7] == null) c.neighbours[7] = getTreeFromPosIfExists(center + new Vector3Int(-chunkSize,0,0));
    }

    void setTopTreeTempOffset() {
        int t = 0;
        foreach (Quadtree c in topTree.children) t += c.tempOffset;
        if (t > 0) {
            topTree.tempOffset = 1;
        } else if (t < 0) {
            topTree.tempOffset = -1;
        } else {
            topTree.tempOffset = 0;
        }
    }

    public static int weightedRandOffset(int o) {
        int[] a = {-1,0,1,o};
        return a[Random.Range(0,4)];
    }

    void initPlayer() {
        byte[] b_player = File.ReadAllBytes("Assets/Entities/player1.png");
        Texture2D t_player = new Texture2D(10,10);
        t_player.LoadImage(b_player);
        t_player.filterMode = FilterMode.Point;
        Sprite s_player = Sprite.Create(t_player, new Rect(0,0,t_player.width,t_player.height),new Vector2(0.5f, 0.5f),10);
        player = new GameObject("player");
        SpriteRenderer sr = player.AddComponent<SpriteRenderer>() as SpriteRenderer;
        sr.sprite = s_player;
        sr.sortingOrder = 1;
        BoxCollider2D collider = player.AddComponent<BoxCollider2D>() as BoxCollider2D;
        //Rigidbody2D rb = player.AddComponent<Rigidbody2D>() as Rigidbody2D;
        //rb.gravityScale = 0f;
        //rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void initTiles() {
        byte[] b_g1 = File.ReadAllBytes("Assets/Tiles/grass1.png");
        byte[] b_g2 = File.ReadAllBytes("Assets/Tiles/grass2.png");
        byte[] b_g3 = File.ReadAllBytes("Assets/Tiles/grass3.png");
        byte[] b_g4 = File.ReadAllBytes("Assets/Tiles/grass4.png");
        byte[] b_g5 = File.ReadAllBytes("Assets/Tiles/grass5.png");

        byte[] b_m1 = File.ReadAllBytes("Assets/Tiles/magicBiome1.png");
        byte[] b_m2 = File.ReadAllBytes("Assets/Tiles/magicBiome2.png");
        byte[] b_m3 = File.ReadAllBytes("Assets/Tiles/magicBiome3.png");
        byte[] b_m4 = File.ReadAllBytes("Assets/Tiles/magicBiome4.png");
        byte[] b_m5 = File.ReadAllBytes("Assets/Tiles/magicBiome5.png");

        Texture2D t_g1 = new Texture2D(10,10);
        Texture2D t_g2 = new Texture2D(10,10);
        Texture2D t_g3 = new Texture2D(10,10);
        Texture2D t_g4 = new Texture2D(10,10);
        Texture2D t_g5 = new Texture2D(10,10);

        Texture2D t_m1 = new Texture2D(10,10);
        Texture2D t_m2 = new Texture2D(10,10);
        Texture2D t_m3 = new Texture2D(10,10);
        Texture2D t_m4 = new Texture2D(10,10);
        Texture2D t_m5 = new Texture2D(10,10);
        
        t_g1.LoadImage(b_g1);
        t_g2.LoadImage(b_g2);
        t_g3.LoadImage(b_g3);
        t_g4.LoadImage(b_g4);
        t_g5.LoadImage(b_g5);

        t_m1.LoadImage(b_m1);
        t_m2.LoadImage(b_m2);
        t_m3.LoadImage(b_m3);
        t_m4.LoadImage(b_m4);
        t_m5.LoadImage(b_m5);

        t_g1.filterMode = FilterMode.Point;
        t_g2.filterMode = FilterMode.Point;
        t_g3.filterMode = FilterMode.Point;
        t_g4.filterMode = FilterMode.Point;
        t_g5.filterMode = FilterMode.Point;

        t_m1.filterMode = FilterMode.Point;
        t_m2.filterMode = FilterMode.Point;
        t_m3.filterMode = FilterMode.Point;
        t_m4.filterMode = FilterMode.Point;
        t_m5.filterMode = FilterMode.Point;

        t_g1.wrapMode = TextureWrapMode.Clamp;
        t_g2.wrapMode = TextureWrapMode.Clamp;
        t_g3.wrapMode = TextureWrapMode.Clamp;
        t_g4.wrapMode = TextureWrapMode.Clamp;
        t_g5.wrapMode = TextureWrapMode.Clamp;

        t_m1.wrapMode = TextureWrapMode.Clamp;
        t_m2.wrapMode = TextureWrapMode.Clamp;
        t_m3.wrapMode = TextureWrapMode.Clamp;
        t_m4.wrapMode = TextureWrapMode.Clamp;
        t_m5.wrapMode = TextureWrapMode.Clamp;

        Sprite s_g1 = Sprite.Create(t_g1, new Rect(0,0,t_g1.width,t_g1.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_g2 = Sprite.Create(t_g2, new Rect(0,0,t_g2.width,t_g2.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_g3 = Sprite.Create(t_g3, new Rect(0,0,t_g3.width,t_g3.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_g4 = Sprite.Create(t_g4, new Rect(0,0,t_g4.width,t_g4.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_g5 = Sprite.Create(t_g5, new Rect(0,0,t_g5.width,t_g5.height),new Vector2(0.5f, 0.5f),10);

        Sprite s_m1 = Sprite.Create(t_m1, new Rect(0,0,t_m1.width,t_m1.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_m2 = Sprite.Create(t_m2, new Rect(0,0,t_m2.width,t_m2.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_m3 = Sprite.Create(t_m3, new Rect(0,0,t_m3.width,t_m3.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_m4 = Sprite.Create(t_m4, new Rect(0,0,t_m4.width,t_m4.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_m5 = Sprite.Create(t_m5, new Rect(0,0,t_m5.width,t_m5.height),new Vector2(0.5f, 0.5f),10);

        for (int i = 0; i < tiles.GetLength(0); i++) {
            for (int j = 0; j < tiles.GetLength(1); j++) {
                tiles[i,j] = (Tile)ScriptableObject.CreateInstance("Tile");
            }
        }

        tiles[3,0].sprite = s_g1;
        tiles[3,1].sprite = s_g2;
        tiles[3,2].sprite = s_g3;
        tiles[3,3].sprite = s_g4;
        tiles[3,4].sprite = s_g5;

        tiles[0,0].sprite = s_m1;
        tiles[0,1].sprite = s_m2;
        tiles[0,2].sprite = s_m3;
        tiles[0,3].sprite = s_m4;
        tiles[0,4].sprite = s_m5;
    }

    Tile[] getTilesForChunk(Chunk c) {
        Tile[] tA = new Tile[c.area.size.x*c.area.size.y];
        int b = c.tempLevel + Chunk.maxTempLevel + 1;
    b = 3;
        if (c.tempLevel == Chunk.magicBiomeTemp) b = 0;
        for (int i = 0; i < tA.Length; i++) {
            tA[i] = tiles[b,Random.Range(0,tiles.GetLength(1))];
        }
        return tA;
    }

    void loadTilesAtChunk(Tile[] tA, Chunk c) {
        map.SetTilesBlock(c.area,tA);

        foreach (var p in c.area.allPositionsWithin) {
            int r = Random.Range(0,4);
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f*r), Vector3.one);
            map.SetTransformMatrix(p,matrix);
            map.RefreshTile(p);
        }
    }

    void addRandomGrassAt(int x, int y) {
        Vector3Int pos = new Vector3Int(x,y,0);
        map.SetTile(pos, tiles[Chunk.maxTempLevel+1,Random.Range(0,tiles.GetLength(0))]);
        int r = Random.Range(0,4);
        Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f*r), Vector3.one);
        map.SetTransformMatrix(pos,matrix);
        map.RefreshTile(pos);
    }

    void addRandomGrassAtBlock(int fromX, int fromY, int toX, int toY) {
        BoundsInt pos = new BoundsInt();
        pos.SetMinMax(new Vector3Int(fromX,fromY,0),new Vector3Int(toX,toY,1));

        Tile[] tA = new Tile[pos.size.x*pos.size.y];
        for (int i = 0; i < tA.Length; i++) {
            tA[i] = tiles[Chunk.maxTempLevel+1,Random.Range(0,tiles.GetLength(1))];
        }

        map.SetTilesBlock(pos,tA);

        foreach (var p in pos.allPositionsWithin) {
            int r = Random.Range(0,4);
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f*r), Vector3.one);
            map.SetTransformMatrix(p,matrix);
            map.RefreshTile(p);
        }
    }

    void addRandomGrassAtChunk(BoundsInt pos) {
        Tile[] tA = new Tile[pos.size.x*pos.size.y];
        for (int i = 0; i < tA.Length; i++) {
            tA[i] = tiles[Chunk.maxTempLevel+1,Random.Range(0,tiles.GetLength(1))];
        }

        map.SetTilesBlock(pos,tA);

        foreach (var p in pos.allPositionsWithin) {
            int r = Random.Range(0,4);
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f*r), Vector3.one);
            map.SetTransformMatrix(p,matrix);
            map.RefreshTile(p);
        }
    }

    void initDebugChunkSquares() {
        Texture2D t_dcs_blue = new Texture2D(chunkSize,chunkSize);
        Texture2D t_dcs_cyan = new Texture2D(chunkSize,chunkSize);
        Texture2D t_dcs_green = new Texture2D(chunkSize,chunkSize);
        Texture2D t_dcs_yellow = new Texture2D(chunkSize,chunkSize);
        Texture2D t_dcs_red = new Texture2D(chunkSize,chunkSize);

        float opacity = 1;
        if (debug_drawTempOffsets) opacity = 0.5f;

        Color[] blues = new Color[chunkSize*chunkSize];
        Color blue = Color.blue;
        blue.a = opacity;
        for (int i = 0; i < chunkSize*chunkSize; i++) {
            blues[i] = blue;
        }
        t_dcs_blue.SetPixels(blues);

        Color[] cyans = new Color[chunkSize*chunkSize];
        Color cyan = Color.cyan;
        cyan.a = opacity;
        for (int i = 0; i < chunkSize*chunkSize; i++) {
            cyans[i] = cyan;
        }
        t_dcs_cyan.SetPixels(cyans);

        Color[] greens = new Color[chunkSize*chunkSize];
        Color green = Color.green;
        green.a = opacity;
        for (int i = 0; i < chunkSize*chunkSize; i++) {
            greens[i] = green;
        }
        t_dcs_green.SetPixels(greens);

        Color[] yellows = new Color[chunkSize*chunkSize];
        Color yellow = Color.yellow;
        yellow.a = opacity;
        for (int i = 0; i < chunkSize*chunkSize; i++) {
            yellows[i] = yellow;
        }
        t_dcs_yellow.SetPixels(yellows);

        Color[] reds = new Color[chunkSize*chunkSize];
        Color red = Color.red;
        red.a = opacity;
        for (int i = 0; i < chunkSize*chunkSize; i++) {
            reds[i] = red;
        }
        t_dcs_red.SetPixels(reds);

        t_dcs_green.Apply();
        t_dcs_cyan.Apply();
        t_dcs_blue.Apply();
        t_dcs_yellow.Apply();
        t_dcs_red.Apply();

        t_dcs_blue.wrapMode = TextureWrapMode.Repeat;
        t_dcs_cyan.wrapMode = TextureWrapMode.Repeat;
        t_dcs_green.wrapMode = TextureWrapMode.Repeat;
        t_dcs_yellow.wrapMode = TextureWrapMode.Repeat;
        t_dcs_red.wrapMode = TextureWrapMode.Repeat;

        Sprite s_dcs_blue = Sprite.Create(t_dcs_blue, new Rect(0,0,chunkSize,chunkSize),new Vector2(0, 0),1);
        Sprite s_dcs_cyan = Sprite.Create(t_dcs_cyan, new Rect(0,0,chunkSize,chunkSize),new Vector2(0, 0),1);
        Sprite s_dcs_green = Sprite.Create(t_dcs_green, new Rect(0,0,chunkSize,chunkSize),new Vector2(0, 0),1);
        Sprite s_dcs_yellow = Sprite.Create(t_dcs_yellow, new Rect(0,0,chunkSize,chunkSize),new Vector2(0, 0),1);
        Sprite s_dcs_red = Sprite.Create(t_dcs_red, new Rect(0,0,chunkSize,chunkSize),new Vector2(0, 0),1);

        debugChunkSquares = new GameObject[5];
        for (int i = 0; i < debugChunkSquares.Length; i++) {
            debugChunkSquares[i] = new GameObject("debugSquare");
            debugChunkSquares[i].SetActive(false);
        }

        SpriteRenderer sr1 = debugChunkSquares[0].AddComponent<SpriteRenderer>() as SpriteRenderer;
        SpriteRenderer sr2 = debugChunkSquares[1].AddComponent<SpriteRenderer>() as SpriteRenderer;
        SpriteRenderer sr3 = debugChunkSquares[2].AddComponent<SpriteRenderer>() as SpriteRenderer;
        SpriteRenderer sr4 = debugChunkSquares[3].AddComponent<SpriteRenderer>() as SpriteRenderer;
        SpriteRenderer sr5 = debugChunkSquares[4].AddComponent<SpriteRenderer>() as SpriteRenderer;

        sr1.sprite = s_dcs_blue;
        sr2.sprite = s_dcs_cyan;
        sr3.sprite = s_dcs_green;
        sr4.sprite = s_dcs_yellow;
        sr5.sprite = s_dcs_red;
    }

    void drawAllTrees() {
        topTree.drawAllTrees();
    }

    void drawTempColors() {
        topTree.drawTempColors();
    }

    void drawTempOffsets() {
        topTree.drawTempOffsets();
    }

    public static void DrawRect(Vector3 min, Vector3 max, Color color, float duration) {
        UnityEngine.Debug.DrawLine(min, new Vector3(min.x, max.y), color, duration);
        UnityEngine.Debug.DrawLine(new Vector3(min.x, max.y), max, color, duration);
        UnityEngine.Debug.DrawLine(max, new Vector3(max.x, min.y), color, duration);
        UnityEngine.Debug.DrawLine(min, new Vector3(max.x, min.y), color, duration);
    }

    public static void DrawX(Vector3 min, Vector3 max, Color color, float duration) {
        UnityEngine.Debug.DrawLine(min, max, color, duration);
        UnityEngine.Debug.DrawLine(new Vector3(min.x, max.y), new Vector3(max.x,min.y), color, duration);
    }
}
