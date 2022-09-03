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

*/

public class Main : MonoBehaviour
{
    GameObject g_map;
    public Tilemap map;
    public Tile[] tiles;
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

    // Start is called before the first frame update
    void Start()
    {
        g_map = new GameObject("map");
        map = g_map.AddComponent<Tilemap>() as Tilemap;
        Grid grid = g_map.AddComponent<Grid>() as Grid;
        TilemapRenderer tr = g_map.AddComponent<TilemapRenderer>() as TilemapRenderer;
        tr.sortingLayerID = SortingLayer.NameToID("mapLayer");
        tiles = new Tile[5];
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
        
        //drawAllTrees();
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
            }
            if (newChunk) lastTree = getTreeFromPos(player.transform.position);
        }
        
        updateSortingOrder();
        //if (time % 100 == 0) drawAllTrees();
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
    }

    void loadChunksAtPlayerPos() {
        loadChunkAtTree(getTreeFromPos(player.transform.position));
        loadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(-chunkSize,chunkSize,0)));
        loadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(0,chunkSize,0)));
        loadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(chunkSize,chunkSize,0)));
        loadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(chunkSize,0,0)));
        loadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(chunkSize,-chunkSize,0)));
        loadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(0,-chunkSize,0)));
        loadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(-chunkSize,-chunkSize,0)));
        loadChunkAtTree(getTreeFromPos(player.transform.position + new Vector3(-chunkSize,0,0)));
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
        //Debug.Log(q.area);
        //Debug.Log(q.level);
        if (!q.getLoaded()) {
            addRandomGrassAtChunk(q.area);
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

    int weightedRandOffset(int o) {
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

        Texture2D t_g1 = new Texture2D(10,10);
        Texture2D t_g2 = new Texture2D(10,10);
        Texture2D t_g3 = new Texture2D(10,10);
        Texture2D t_g4 = new Texture2D(10,10);
        Texture2D t_g5 = new Texture2D(10,10);
        
        t_g1.LoadImage(b_g1);
        t_g2.LoadImage(b_g2);
        t_g3.LoadImage(b_g3);
        t_g4.LoadImage(b_g4);
        t_g5.LoadImage(b_g5);

        t_g1.filterMode = FilterMode.Point;
        t_g2.filterMode = FilterMode.Point;
        t_g3.filterMode = FilterMode.Point;
        t_g4.filterMode = FilterMode.Point;
        t_g5.filterMode = FilterMode.Point;

        t_g1.wrapMode = TextureWrapMode.Clamp;
        t_g2.wrapMode = TextureWrapMode.Clamp;
        t_g3.wrapMode = TextureWrapMode.Clamp;
        t_g4.wrapMode = TextureWrapMode.Clamp;
        t_g5.wrapMode = TextureWrapMode.Clamp;

        Sprite s_g1 = Sprite.Create(t_g1, new Rect(0,0,t_g1.width,t_g1.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_g2 = Sprite.Create(t_g2, new Rect(0,0,t_g2.width,t_g2.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_g3 = Sprite.Create(t_g3, new Rect(0,0,t_g3.width,t_g3.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_g4 = Sprite.Create(t_g4, new Rect(0,0,t_g4.width,t_g4.height),new Vector2(0.5f, 0.5f),10);
        Sprite s_g5 = Sprite.Create(t_g5, new Rect(0,0,t_g5.width,t_g5.height),new Vector2(0.5f, 0.5f),10);

        for (int i = 0; i < tiles.Length; i++) {
            tiles[i] = (Tile)ScriptableObject.CreateInstance("Tile");
        }

        tiles[0].sprite = s_g1;
        tiles[1].sprite = s_g2;
        tiles[2].sprite = s_g3;
        tiles[3].sprite = s_g4;
        tiles[4].sprite = s_g5;
    }

    void addRandomGrassAt(int x, int y) {
        Vector3Int pos = new Vector3Int(x,y,0);
        map.SetTile(pos, tiles[Random.Range(0,tiles.Length)]);
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
            tA[i] = tiles[Random.Range(0,tiles.Length)];
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
            tA[i] = tiles[Random.Range(0,tiles.Length)];
        }

        map.SetTilesBlock(pos,tA);

        foreach (var p in pos.allPositionsWithin) {
            int r = Random.Range(0,4);
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f*r), Vector3.one);
            map.SetTransformMatrix(p,matrix);
            map.RefreshTile(p);
        }
    }

    void drawAllTrees() {
        topTree.drawAllTrees();
    }

    public static void DrawRect(Vector3 min, Vector3 max, Color color, float duration) {
        UnityEngine.Debug.DrawLine(min, new Vector3(min.x, max.y), color, duration);
        UnityEngine.Debug.DrawLine(new Vector3(min.x, max.y), max, color, duration);
        UnityEngine.Debug.DrawLine(max, new Vector3(max.x, min.y), color, duration);
        UnityEngine.Debug.DrawLine(min, new Vector3(max.x, min.y), color, duration);
    }
}
