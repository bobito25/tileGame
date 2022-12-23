using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;
using System;

/*

TODO:

-- change humidity to fit more than two biomes per temp

-- remake character

-- remake player animation

-- add semi chunks that can be multiple temps

-- turn map into seperate tilemaps at a certain quadtree level and disable far away tilemaps for performance

-- make more obstacles (trees, stones)

-- fix neighbours being checked more than needed (not very important)

-- optimize placing obstacles (current implementation keeps randomly placing until no overlap -> inefficient for large num of obstacles in chunk)
 -> kinda done: will try to place 3 times before giving up

-- make trees spawn in groups (sometimes?)

*/

public class Main : MonoBehaviour
{
    GameObject g_map;
    public static Tilemap map;
    public Tile[,] tiles;
    public static Tile[] debugTiles;
    public Quadtree topTree;

    Textures textures;

    public Vector2 seed;
    public static float scale = 20f; // higher -> less movement in perlin noise -> bigger temp zones

    public static int chunkSize = 16; //length of one side -> should be even
    public static int quadtreeMaxLevel = 1; // starts at one
    public static int quadtreeSideLength = chunkSize * (int)Mathf.Pow(2,quadtreeMaxLevel-1);
    public static int borderSize = 4; // interpolation border (where biomes mix)
    static float[] preCalcedFuncVals; // y = 0.5 * e^(-x) for 0 <= x <= chunkSize/2 (clamped for x >= 8)
    public static int loadDistance = 3;
    public static int preloadDistance = loadDistance+1;
    public Queue<Loadable> toLoad;

    public GameObject player;
    public Animator playerAnim;
    public int playerSpeed;
    Vector3 nextMove;
    Quadtree lastTree;

    public static GameObject[] obstacles; // first obstacles that are cloned
    public static int obstaclesPerTile = chunkSize;

    public static int time;

    public static GameObject[] debugChunkSquares;

    public bool autoWalk = false;
    public int autoWalkCounter = 0;
    public int autoWalkDir = 0;
    public int autoWalkLength = 30;

    public bool debug_drawTrees = false;
    public bool debug_drawTempColors = false;
    public bool debug_drawHumidity = false;


    // Start is called before the first frame update
    void Start()
    {
        g_map = new GameObject("map");
        map = g_map.AddComponent<Tilemap>() as Tilemap;
        Grid grid = g_map.AddComponent<Grid>() as Grid;
        TilemapRenderer tr = g_map.AddComponent<TilemapRenderer>() as TilemapRenderer;
        tr.sortingLayerID = SortingLayer.NameToID("mapLayer");

        seed = new Vector2(UnityEngine.Random.value*UnityEngine.Random.Range(1,100000),UnityEngine.Random.value*UnityEngine.Random.Range(1,100000));

        textures = new Textures();

        initTiles();
        initTrees();
        initEntities();
        initPreCalcedFuncVals();
        Biome.initBiomes();

        nextMove = new Vector3(0,0,0);
        playerSpeed = 10;

        time = 0;

        Camera.main.orthographicSize = 10;
        
        toLoad = new Queue<Loadable>();

        loadChunksAtPlayerPos();
        removeObstaclesAtPlayerPos();
        lastTree = getTreeFromPos(player.transform.position);
        
        initDebugChunkSquares();
        if (debug_drawTrees) drawAllTrees();
        if (debug_drawTempColors) drawTempColors();
        if (debug_drawHumidity) drawHumidity();
        //Debug.DrawLine(new Vector3(0,0,0), new Vector3(1,1,0), Color.red, 100000000, false);

        //TileBase[] allTiles = map.GetTilesBlock(map.cellBounds);
    }

    // Update is called once per frame
    void Update()
    {
        resetMove();
        if (Input.GetMouseButtonDown(0)) {
            Chunk playerC = getTreeFromPos(player.transform.position);
            Debug.Log(playerC.pos);
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
        if (toLoad.Count > 0) toLoad.Dequeue().load();
    }

    void FixedUpdate()
    {
        time++;

        if (autoWalk) {
            if (autoWalkCounter >= autoWalkLength) autoWalk = false;
            if (time % 30 == 0) {
                if (autoWalkDir == 0) {
                    player.transform.Translate(new Vector3(0,chunkSize,0));
                    if (player.transform.position.y >= chunkSize*autoWalkLength) {
                        autoWalkDir = 1;
                        autoWalkCounter++;
                    }
                } else if (autoWalkDir == 1){
                    player.transform.Translate(new Vector3(chunkSize,0,0));
                    if (player.transform.position.x >= chunkSize*autoWalkCounter*3) autoWalkDir = 2;
                } else if (autoWalkDir == 2){
                    player.transform.Translate(new Vector3(0,-chunkSize,0));
                    if (player.transform.position.y <= 0) {
                        autoWalkDir = 3;
                        autoWalkCounter++;
                    }
                } else {
                    player.transform.Translate(new Vector3(chunkSize,0,0));
                    if (player.transform.position.x >= chunkSize*autoWalkCounter*3) autoWalkDir = 0;
                }
            }
        }
        
        if (time % 10 == 0) {
            bool newChunk = lastTree != getTreeFromPos(player.transform.position);
            if (newChunk) {
                loadChunksAtPlayerPos();
                unloadChunksAroundPlayerPos();
                if (debug_drawTrees) drawAllTrees();
                if (debug_drawTempColors) drawTempColors();
                if (debug_drawHumidity) drawHumidity();
                lastTree = getTreeFromPos(player.transform.position);
            }
        }
        
        updateSortingOrder();
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
        //initStoneObs();
    }

    void initTreeObs() {
        Sprite s_tree1 = Sprite.Create(textures.obstacleTextures[0], new Rect(0,0,textures.obstacleTextures[0].width,textures.obstacleTextures[0].height),new Vector2(0f, 0f),20);
        GameObject firstTree1 = new GameObject("tree");
        firstTree1.layer = 10;
        firstTree1.SetActive(false);
        SpriteRenderer sr1 = firstTree1.AddComponent<SpriteRenderer>() as SpriteRenderer;
        sr1.sprite = s_tree1;
        BoxCollider2D collider1 = firstTree1.AddComponent<BoxCollider2D>() as BoxCollider2D;
        collider1.size = new Vector2(1,1);
        obstacles[0] = firstTree1;

        Sprite s_tree1_f = Sprite.Create(textures.obstacleTextures[1], new Rect(0,0,textures.obstacleTextures[1].width,textures.obstacleTextures[1].height),new Vector2(0f, 0f),20);
        GameObject firstTree1_f = new GameObject("tree");
        firstTree1_f.layer = 10;
        firstTree1_f.SetActive(false);
        SpriteRenderer sr1_f = firstTree1_f.AddComponent<SpriteRenderer>() as SpriteRenderer;
        sr1_f.sprite = s_tree1_f;
        BoxCollider2D collider1_f = firstTree1_f.AddComponent<BoxCollider2D>() as BoxCollider2D;
        collider1_f.size = new Vector2(1,1);
        obstacles[1] = firstTree1_f;
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
        if (m.magnitude == 0) {
            playerAnim.SetBool("moving",false);
            return;
        }
        Vector3 mX = new Vector3(m.x,0,0);
        Vector3 mY = new Vector3(0,m.y,0);
        RaycastHit2D hit = Physics2D.Raycast(player.transform.position, m, 0.5f, LayerMask.GetMask("obstacles"));
        if (hit.collider == null) {
            player.transform.Translate(m);
            Camera.main.transform.position = player.transform.position - new Vector3(0,0,10);
            playerAnim.SetBool("moving",true);
        } else if (Physics2D.Raycast(player.transform.position, mX, 0.5f, LayerMask.GetMask("obstacles")).collider == null) {
            player.transform.Translate(mX);
            Camera.main.transform.position = player.transform.position - new Vector3(0,0,10);
            playerAnim.SetBool("moving",true);
        } else if (Physics2D.Raycast(player.transform.position, mY, 0.5f, LayerMask.GetMask("obstacles")).collider == null) {
            player.transform.Translate(mY);
            Camera.main.transform.position = player.transform.position - new Vector3(0,0,10);
            playerAnim.SetBool("moving",true);
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
        ((Chunk)topTree).tempIndex = Chunk.maxTempLevel+1;
        ((Chunk)topTree).hasTemp = true;
    }

    void loadChunksAtPlayerPos() {
        Chunk playerC = getTreeFromPos(player.transform.position);
        // make sure chunks exist in quadtree in spiral
        for (int r = 1; r <= preloadDistance; r++) {
            int tileR = chunkSize * r;
            Vector3 curP = player.transform.position + new Vector3(-tileR,tileR,0);
            int d = r * 2;
            for (int i = 0; i < d; i++) {
                getTreeFromPos(curP);
                curP.x += chunkSize;
            }
            for (int i = 0; i < d; i++) {
                getTreeFromPos(curP);
                curP.y -= chunkSize;
            }
            for (int i = 0; i < d; i++) {
                getTreeFromPos(curP);
                curP.x -= chunkSize;
            }
            for (int i = 0; i < d; i++) {
                getTreeFromPos(curP);
                curP.y += chunkSize;
            }
        }
        updateNeighbours(playerC);
        // update neighbours of chunks in spiral
        for (int r = 1; r <= preloadDistance; r++) {
            int tileR = chunkSize * r;
            Vector3 startP = player.transform.position + new Vector3(-tileR,tileR,0);
            Chunk curC = getTreeFromPos(startP);
            int d = r * 2;
            for (int i = 0; i < d; i++) {
                updateNeighbours(curC);
                curC = curC.neighbours[3];
            }
            for (int i = 0; i < d; i++) {
                updateNeighbours(curC);
                curC = curC.neighbours[5];
            }
            for (int i = 0; i < d; i++) {
                updateNeighbours(curC);
                curC = curC.neighbours[7];
            }
            for (int i = 0; i < d; i++) {
                updateNeighbours(curC);
                curC = curC.neighbours[1];
            }
        }
        setPos(playerC);
        // set pos coords of chunks in spiral
        for (int r = 1; r <= preloadDistance; r++) {
            int tileR = chunkSize * r;
            Vector3 startP = player.transform.position + new Vector3(-tileR,tileR,0);
            Chunk curC = getTreeFromPos(startP);
            int d = r * 2;
            for (int i = 0; i < d; i++) {
                setPos(curC);
                curC = curC.neighbours[3];
            }
            for (int i = 0; i < d; i++) {
                setPos(curC);
                curC = curC.neighbours[5];
            }
            for (int i = 0; i < d; i++) {
                setPos(curC);
                curC = curC.neighbours[7];
            }
            for (int i = 0; i < d; i++) {
                setPos(curC);
                curC = curC.neighbours[1];
            }
        }
        setTreeTempLevel(playerC);
        setBiome(playerC);
        // set chunk temp levels in spiral
        for (int r = 1; r <= preloadDistance; r++) {
            int tileR = chunkSize * r;
            Vector3 startP = player.transform.position + new Vector3(-tileR,tileR,0);
            Chunk curC = getTreeFromPos(startP);
            int d = r * 2;
            for (int i = 0; i < d; i++) {
                setTreeTempLevel(curC);
                setBiome(curC);
                curC = curC.neighbours[3];
            }
            for (int i = 0; i < d; i++) {
                setTreeTempLevel(curC);
                setBiome(curC);
                curC = curC.neighbours[5];
            }
            for (int i = 0; i < d; i++) {
                setTreeTempLevel(curC);
                setBiome(curC);
                curC = curC.neighbours[7];
            }
            for (int i = 0; i < d; i++) {
                setTreeTempLevel(curC);
                setBiome(curC);
                curC = curC.neighbours[1];
            }
        }

        // (graphically) load chunks
        loadChunkAtTree(playerC);
        for (int r = 1; r <= loadDistance; r++) {
            int tileR = chunkSize * r;
            Vector3 startP = player.transform.position + new Vector3(-tileR,tileR,0);
            Chunk curC = getTreeFromPos(startP);
            int d = r * 2;
            for (int i = 0; i < d; i++) {
                loadChunkAtTree(curC);
                curC = curC.neighbours[3];
            }
            for (int i = 0; i < d; i++) {
                loadChunkAtTree(curC);
                curC = curC.neighbours[5];
            }
            for (int i = 0; i < d; i++) {
                loadChunkAtTree(curC);
                curC = curC.neighbours[7];
            }
            for (int i = 0; i < d; i++) {
                loadChunkAtTree(curC);
                curC = curC.neighbours[1];
            }
        }
    }

    void setPos(Chunk c) {
        if (!c.hasPos) {
            c.pos = new Vector2Int((int)c.area.center.x / chunkSize,(int)c.area.center.y / chunkSize);
            c.hasPos = true;
        }
    }

    void unsetNeighboursTemp(Chunk c) {
        foreach (Chunk n in c.neighbours) {
            n.hasTemp = false;
        }
    }

    void unloadChunksAroundPlayerPos() {
        if (loadDistance == 1) {
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
        } else {
            int r = loadDistance + 1;
            int tileR = chunkSize * r;
            Vector3 startP = player.transform.position + new Vector3(-tileR,tileR,0);
            Chunk curC = getTreeFromPos(startP);
            int d = r * 2;
            for (int i = 0; i < d; i++) {
                unloadChunkAtTree(curC);
                curC = curC.neighbours[3];
            }
            for (int i = 0; i < d; i++) {
                unloadChunkAtTree(curC);
                curC = curC.neighbours[5];
            }
            for (int i = 0; i < d; i++) {
                unloadChunkAtTree(curC);
                curC = curC.neighbours[7];
            }
            for (int i = 0; i < d; i++) {
                unloadChunkAtTree(curC);
                curC = curC.neighbours[1];
            }
        }
    }

    void unloadChunkAtTree(Chunk q) {
        if (q.getLoaded()) {
            q.unloadObstacles();
        }
    }

    void loadChunkAtTree(Chunk q) {
        if (!q.getLoaded()) {
            loadTilesAtChunk(q);
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
        for (int i = 0; i < Biome.obstacleNumPerBiomePerTemp[q.tempIndex-1][q.biome.biomeNum]; i++) {
            placeObstacle(q,os,3);
        }
        q.setObstacles(os);
    }

    void placeObstacle(Chunk c, List<Obstacle> os, int tryNum) {
        if (tryNum == 0) return;
        BoundsInt area = c.area;
        Vector2Int pos = new Vector2Int(UnityEngine.Random.Range(area.x,area.xMax),UnityEngine.Random.Range(area.y,area.yMax));
        if (checkFree(pos, os)) {
            os.Add(spawnObstacleAt(pos,c.biome.getRandomPossibleObstacleIndex()));
        } else {
            placeObstacle(c,os,tryNum-1);
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
        GameObject newO = UnityEngine.Object.Instantiate(obstacles[type]);
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
                if (old.level > Biome.humidityLevel) {
                    topTree.children[0].humidity = weightedRandOffset(old.humidity);
                    topTree.children[1].humidity = weightedRandOffset(old.humidity);
                    topTree.children[3].humidity = weightedRandOffset(old.humidity);
                    setTopTreeHumidity();
                } else {
                    topTree.humidity = 0;
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
                if (old.level > Biome.humidityLevel) {
                    topTree.children[0].humidity = weightedRandOffset(old.humidity);
                    topTree.children[2].humidity = weightedRandOffset(old.humidity);
                    topTree.children[3].humidity = weightedRandOffset(old.humidity);
                    setTopTreeHumidity();
                } else {
                    topTree.humidity = 0;
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
                if (old.level > Biome.humidityLevel) {
                    topTree.children[0].humidity = weightedRandOffset(old.humidity);
                    topTree.children[1].humidity = weightedRandOffset(old.humidity);
                    topTree.children[2].humidity = weightedRandOffset(old.humidity);
                    setTopTreeHumidity();
                } else {
                    topTree.humidity = 0;
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
                if (old.level > Biome.humidityLevel) {
                    topTree.children[2].humidity = weightedRandOffset(old.humidity);
                    topTree.children[1].humidity = weightedRandOffset(old.humidity);
                    topTree.children[3].humidity = weightedRandOffset(old.humidity);
                    setTopTreeHumidity();
                } else {
                    topTree.humidity = 0;
                }
            }
        }
    }

    bool setTreeTempLevel(Chunk c) {
        if (c.hasTemp) return true;

        float x = (((float)c.pos.x)/scale)+seed.x;
        float y = (((float)c.pos.y)/scale)+seed.y;
        float noise = Mathf.PerlinNoise(x,y);
        float d = 1f/Chunk.numTempLevels;
        for (int i = 1; i <= Chunk.numTempLevels; i++) {
            if (noise < i*d) {
                setTempByIndex(c,i);
                return true;
            }
        }
        setTemp(c,Chunk.maxTempLevel);
        return true;
    }

    void setTemp(Chunk c, int t) {
        c.tempLevel = t;
        if (c.tempLevel == Chunk.magicBiomeTemp) {
            c.tempIndex = 0;
        } else {
            c.tempIndex = t + Chunk.maxTempLevel + 1;
        }
        c.hasTemp = true;
    }

    void setTempByIndex(Chunk c, int t) {
        c.tempIndex = t;
        if (c.tempIndex == 0) {
            c.tempLevel = Chunk.magicBiomeTemp;
        } else {
            c.tempLevel = t - Chunk.maxTempLevel - 1;
        }
        c.hasTemp = true;
    }

    void setRandomBiome(Chunk c) {
        c.biome = new Biome(c.tempIndex,UnityEngine.Random.Range(0,Biome.numBiomesPerTemp[c.tempIndex-1]));
    }

    void setBiome(Chunk c) {
        if (c.humidity == -1) {
            c.biome = new Biome(c.tempIndex,0);
        } else if (c.humidity == 1) {
            c.biome = new Biome(c.tempIndex,1);
        } else {
            if (UnityEngine.Random.value < 0.5f) {
                c.biome = new Biome(c.tempIndex,0);
            } else {
                c.biome = new Biome(c.tempIndex,1);
            }
        }
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

    void setTopTreeHumidity() {
        int t = 0;
        foreach (Quadtree c in topTree.children) t += c.humidity;
        if (t > 0) {
            topTree.humidity = 1;
        } else if (t < 0) {
            topTree.humidity = -1;
        } else {
            topTree.humidity = 0;
        }
    }

    public static int weightedRandOffset(int o) {
        int[] a = {-1,0,1,o};
        return a[UnityEngine.Random.Range(0,4)];
    }

    void initPlayer() {
        Sprite s_player = Sprite.Create(textures.playerTexture, new Rect(0,0,textures.playerTexture.width,textures.playerTexture.height),new Vector2(0.5f, 0.5f),10);
        player = GameObject.Find("player");
        SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
        //sr.sprite = s_player;
        sr.sortingOrder = 1;
        BoxCollider2D collider = player.AddComponent<BoxCollider2D>() as BoxCollider2D;
        //Rigidbody2D rb = player.AddComponent<Rigidbody2D>() as Rigidbody2D;
        //rb.gravityScale = 0f;
        //rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        playerAnim = player.GetComponent<Animator>() as Animator;
    }

    void initTiles() {
        tiles = new Tile[Textures.numTileGroups,Textures.numTilesPerGroup];

        Sprite[,] tileSprites = new Sprite[Textures.numTileGroups,Textures.numTilesPerGroup];
        
        Rect r = new Rect(0,0,10,10);
        Rect r2 = new Rect(0,0,20,20);
        Vector2 v = new Vector2(0.5f,0.5f);

        for (int i = 0; i < Textures.numTileGroups; i++) {
            for (int j = 0; j < Textures.numTilesPerGroup; j++) {
                tileSprites[i,j] = Sprite.Create(textures.tileTextures[i,j],r2,v,20);
                tiles[i,j] = (Tile)ScriptableObject.CreateInstance("Tile");
                tiles[i,j].sprite = tileSprites[i,j];
            }
        }

        //debugTiles

        debugTiles = new Tile[1];

        byte[] b_dT1 = File.ReadAllBytes("Assets/Tiles/debugTile10x10.png");
        Texture2D t_dT1 = new Texture2D(10,10);
        t_dT1.LoadImage(b_dT1);
        t_dT1.filterMode = FilterMode.Point;
        t_dT1.wrapMode = TextureWrapMode.Clamp;
        Sprite s_dT1 = Sprite.Create(t_dT1, r, v, 10);
        debugTiles[0] = (Tile)ScriptableObject.CreateInstance("Tile");
        debugTiles[0].sprite = s_dT1;
    }

    void loadTilesAtSection(Chunk c, int s) {
        BoundsInt area = c.sections[s];
        Tile[] tA = new Tile[area.size.x*area.size.y];

        int curTI = c.tempIndex;
        if (s == 0) {
            int topTI = c.checkNeighbourTempIndex(0);
            int leftTI = c.checkNeighbourTempIndex(3);
            for (int i = 0; i < borderSize; i++) {
                int w1 = borderSize-(i+1);
                for (int j = 0; j < borderSize; j++) {
                    tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,topTI,leftTI,w1,j);
                }
            }
        } else if (s == 1) {
            int topTI = c.checkNeighbourTempIndex(0);
            for (int i = 0; i < borderSize; i++) {
                int ii = i * area.size.x;
                int w = borderSize-(i+1);
                for (int j = 0; j < area.size.x; j++) {
                    tA[ii+j] = weightedRandTile(curTI,topTI,w);
                }
            }
        } else if (s == 2) {
            int topTI = c.checkNeighbourTempIndex(0);
            int rightTI = c.checkNeighbourTempIndex(1);
            for (int i = 0; i < borderSize; i++) {
                int w1 = borderSize-(i+1);
                for (int j = 0; j < borderSize; j++) {
                    tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,topTI,rightTI,w1,borderSize-(j+1));
                }
            }
        } else if (s == 3) {
            int rightTI = c.checkNeighbourTempIndex(1);
            for (int j = borderSize-1; j >= 0; j--) {
                int w = borderSize-(j+1);
                for (int i = 0; i < area.size.y; i++) {
                    tA[(i*borderSize)+j] = weightedRandTile(curTI,rightTI,w);
                }
            }
        } else if (s == 4) {
            int botTI = c.checkNeighbourTempIndex(2);
            int rightTI = c.checkNeighbourTempIndex(1);
            for (int i = 0; i < borderSize; i++) {
                for (int j = 0; j < borderSize; j++) {
                    tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,botTI,rightTI,i,borderSize-(j+1));
                }
            }
        } else if (s == 5) {
            int botTI = c.checkNeighbourTempIndex(2);
            for (int i = 0; i < borderSize; i++) {
                int ii = i * area.size.x;
                for (int j = 0; j < area.size.x; j++) {
                    tA[ii+j] = weightedRandTile(curTI,botTI,i);
                }
            }
        } else if (s == 6) {
            int botTI = c.checkNeighbourTempIndex(2);
            int leftTI = c.checkNeighbourTempIndex(3);
            for (int i = 0; i < borderSize; i++) {
                for (int j = 0; j < borderSize; j++) {
                    tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,botTI,leftTI,i,j);
                }
            }
        } else if (s == 7) {
            int leftTI = c.checkNeighbourTempIndex(1);
            for (int j = borderSize-1; j >= 0; j--) {
                for (int i = 0; i < area.size.y; i++) {
                    tA[(i*borderSize)+j] = weightedRandTile(curTI,leftTI,j);
                }
            }
        } else if (s == 8) {
            for (int i = 0; i < tA.Length; i++) tA[i] = tiles[curTI,UnityEngine.Random.Range(0,tiles.GetLength(1))];
        } else {
            Debug.Log("e: section int not valid (loadTilesAtSection in Main)");
        }

        toLoad.Enqueue(new Loadable(tA,area));

        foreach (var p in c.area.allPositionsWithin) {
            int r = UnityEngine.Random.Range(0,4);
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f*r), Vector3.one);
            map.SetTransformMatrix(p,matrix);
            map.RefreshTile(p);
        }
    }

    void loadTilesAtSide(Chunk c, int side) {
        int curTI = c.tempIndex;
        int topTI = c.checkNeighbourTempIndex(0);
        int rightTI = c.checkNeighbourTempIndex(1);
        int botTI = c.checkNeighbourTempIndex(2);
        int leftTI = c.checkNeighbourTempIndex(3);

        int[] sections;
        if (side == 0) {
            sections = new int[] {0,1,2};
        } else if (side == 1) {
            sections = new int[] {2,3,4};
        } else if (side == 2) {
            sections = new int[] {4,5,6};
        } else if (side == 3) {
            sections = new int[] {6,7,0};
        } else {
            sections = null;
            Debug.Log("e: side int not valid (loadTilesAtSide in Main)");
        }

        foreach (int s in sections)  {
            BoundsInt area = c.sections[s];
            Tile[] tA = new Tile[area.size.x*area.size.y];

            if (s == 0) {
                for (int i = 0; i < borderSize; i++) {
                    int w1 = borderSize-(i+1);
                    for (int j = 0; j < borderSize; j++) {
                        tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,topTI,leftTI,w1,j);
                    }
                }
            } else if (s == 1) {
                for (int i = 0; i < borderSize; i++) {
                    int ii = i * area.size.x;
                    int w = borderSize-(i+1);
                    for (int j = 0; j < area.size.x; j++) {
                        tA[ii+j] = weightedRandTile(curTI,topTI,w);
                    }
                }
            } else if (s == 2) {
                for (int i = 0; i < borderSize; i++) {
                    int w1 = borderSize-(i+1);
                    for (int j = 0; j < borderSize; j++) {
                        tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,topTI,rightTI,w1,borderSize-(j+1));
                    }
                }
            } else if (s == 3) {
                for (int j = borderSize-1; j >= 0; j--) {
                    int w = borderSize-(j+1);
                    for (int i = 0; i < area.size.y; i++) {
                        tA[(i*borderSize)+j] = weightedRandTile(curTI,rightTI,w);
                    }
                }
            } else if (s == 4) {
                for (int i = 0; i < borderSize; i++) {
                    for (int j = 0; j < borderSize; j++) {
                        tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,botTI,rightTI,i,borderSize-(j+1));
                    }
                }
            } else if (s == 5) {
                for (int i = 0; i < borderSize; i++) {
                    int ii = i * area.size.x;
                    for (int j = 0; j < area.size.x; j++) {
                        tA[ii+j] = weightedRandTile(curTI,botTI,i);
                    }
                }
            } else if (s == 6) {
                for (int i = 0; i < borderSize; i++) {
                    for (int j = 0; j < borderSize; j++) {
                        tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,botTI,leftTI,i,j);
                    }
                }
            } else if (s == 7) {
                for (int j = borderSize-1; j >= 0; j--) {
                    for (int i = 0; i < area.size.y; i++) {
                        tA[(i*borderSize)+j] = weightedRandTile(curTI,leftTI,j);
                    }
                }
            } else if (s == 8) {
                for (int i = 0; i < tA.Length; i++) tA[i] = tiles[curTI,UnityEngine.Random.Range(0,tiles.GetLength(1))];
            }

            toLoad.Enqueue(new Loadable(tA,area));

            foreach (var p in c.area.allPositionsWithin) {
                int r = UnityEngine.Random.Range(0,4);
                Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f*r), Vector3.one);
                map.SetTransformMatrix(p,matrix);
                map.RefreshTile(p);
            }
        }
    }

    void loadTilesAtChunk(Chunk c) {
        //TI = TempIndex
        int curTI = c.tempIndex;
        int topTI = c.checkNeighbourTempIndex(0);
        int rightTI = c.checkNeighbourTempIndex(1);
        int botTI = c.checkNeighbourTempIndex(2);
        int leftTI = c.checkNeighbourTempIndex(3);
        
        BoundsInt area = c.sections[0];
        Tile[] tA = new Tile[area.size.x*area.size.y];

        for (int i = 0; i < borderSize; i++) {
            int w1 = borderSize-(i+1);
            for (int j = 0; j < borderSize; j++) {
                tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,topTI,leftTI,w1,j);
            }
        }

        toLoad.Enqueue(new Loadable(tA,area));

        area = c.sections[1];
        tA = new Tile[area.size.x*area.size.y];
        
        for (int i = 0; i < borderSize; i++) {
            int ii = i * area.size.x;
            int w = borderSize-(i+1);
            for (int j = 0; j < area.size.x; j++) {
                tA[ii+j] = weightedRandTile(curTI,topTI,w);
            }
        }

        toLoad.Enqueue(new Loadable(tA,area));
        
        area = c.sections[2];
        tA = new Tile[area.size.x*area.size.y];

        for (int i = 0; i < borderSize; i++) {
            int w1 = borderSize-(i+1);
            for (int j = 0; j < borderSize; j++) {
                tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,topTI,rightTI,w1,borderSize-(j+1));
            }
        }

        toLoad.Enqueue(new Loadable(tA,area));
        
        area = c.sections[3];
        tA = new Tile[area.size.x*area.size.y];

        for (int j = borderSize-1; j >= 0; j--) {
            int w = borderSize-(j+1);
            for (int i = 0; i < area.size.y; i++) {
                tA[(i*borderSize)+j] = weightedRandTile(curTI,rightTI,w);
            }
        }

        toLoad.Enqueue(new Loadable(tA,area));
        
        area = c.sections[4];
        tA = new Tile[area.size.x*area.size.y];

        for (int i = 0; i < borderSize; i++) {
            for (int j = 0; j < borderSize; j++) {
                tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,botTI,rightTI,i,borderSize-(j+1));
            }
        }

        toLoad.Enqueue(new Loadable(tA,area));
        
        area = c.sections[5];
        tA = new Tile[area.size.x*area.size.y];

        for (int i = 0; i < borderSize; i++) {
            int ii = i * area.size.x;
            for (int j = 0; j < area.size.x; j++) {
                tA[ii+j] = weightedRandTile(curTI,botTI,i);
            }
        }

        toLoad.Enqueue(new Loadable(tA,area));
        
        area = c.sections[6];
        tA = new Tile[area.size.x*area.size.y];

        for (int i = 0; i < borderSize; i++) {
            for (int j = 0; j < borderSize; j++) {
                tA[(i*borderSize)+j] = doubleWeightedRandTile(curTI,botTI,leftTI,i,j);
            }
        }

        toLoad.Enqueue(new Loadable(tA,area));
        
        area = c.sections[7];
        tA = new Tile[area.size.x*area.size.y];

        for (int j = borderSize-1; j >= 0; j--) {
            for (int i = 0; i < area.size.y; i++) {
                tA[(i*borderSize)+j] = weightedRandTile(curTI,leftTI,j);
            }
        }

        toLoad.Enqueue(new Loadable(tA,area));
        
        area = c.sections[8];
        tA = new Tile[area.size.x*area.size.y];

        for (int i = 0; i < tA.Length; i++) tA[i] = tiles[curTI,UnityEngine.Random.Range(0,tiles.GetLength(1))];

        toLoad.Enqueue(new Loadable(tA,area));

        foreach (var p in c.area.allPositionsWithin) {
            int r = UnityEngine.Random.Range(0,4);
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f*r), Vector3.one);
            map.SetTransformMatrix(p,matrix);
            map.RefreshTile(p);
        }
    }

    void loadTilesAtBounds(Tile[] tA, BoundsInt b) {
        map.SetTilesBlock(b,tA);

        foreach (var p in b.allPositionsWithin) {
            int r = UnityEngine.Random.Range(0,4);
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f*r), Vector3.one);
            map.SetTransformMatrix(p,matrix);
            map.RefreshTile(p);
        }
    }

    Tile weightedRandTile(int t, int nT, int w) { // t and nT are tempIndex not tempLevel
        if (t == nT) return tiles[t,UnityEngine.Random.Range(0,tiles.GetLength(1))];
        // w : weight from 0 to chunkSize/2 (distance to next chunk)
        if (UnityEngine.Random.value < preCalcedFuncVals[w]) {
            return tiles[nT,UnityEngine.Random.Range(0,tiles.GetLength(1))];
        } else {
            return tiles[t,UnityEngine.Random.Range(0,tiles.GetLength(1))];
        }
    }

    Tile doubleWeightedRandTile(int t, int nT1, int nT2, int w1, int w2) { // t, nT1/2 tempIndex not tempLevel; w1/2 : weight from 0 to chunkSize/2 (distance to next chunk)
        //if (t == nT1) return weightedRandTile(t,nT2,w2);
        //if (t == nT2) return weightedRandTile(t,nT1,w1);
        int smallerW = w1; //more important
        int nT01 = nT1;
        int biggerW = w2; //less important
        int nT02 = nT2;
        if (w2 < w1) {
            smallerW = w2;
            nT01 = nT2;
            biggerW = w1;
            nT02 = nT1;
        }
        float r = UnityEngine.Random.value;
        if (r < preCalcedFuncVals[smallerW]) {
            return tiles[nT01,UnityEngine.Random.Range(0,tiles.GetLength(1))];
        } else {
            if (r < preCalcedFuncVals[biggerW]+preCalcedFuncVals[smallerW]) {
                return tiles[nT02,UnityEngine.Random.Range(0,tiles.GetLength(1))];
            } else {
                return tiles[t,UnityEngine.Random.Range(0,tiles.GetLength(1))];
            }
        }
    }

    void initPreCalcedFuncVals() {
        int arrLength = Math.Max(borderSize,8);
        preCalcedFuncVals = new float[arrLength];
        for (int i = 0; i < 8; i++) {
            preCalcedFuncVals[i] = 0.5f * (Mathf.Exp(-(float)i));
        }
        for (int i = 8; i < arrLength; i++) {
            preCalcedFuncVals[i] = 0;
        }
    }

    void updatePartiallyLoadedSides(Chunk c) { //theoretically unneeded
        if (!c.partiallyLoaded) return;
        for (int i = 0; i < 4; i++) {
            Chunk n = c.neighbours[(2*i)+1];
            if (c.unloadedSides[i] && n != null && n.hasTemp) {
                loadTilesAtSide(c,i);
                c.unloadedSides[i] = false;
                c.checkDoneLoading();
            }
        }
    }


    void addRandomGrassAt(int x, int y) {
        Vector3Int pos = new Vector3Int(x,y,0);
        map.SetTile(pos, tiles[Chunk.maxTempLevel+1,UnityEngine.Random.Range(0,tiles.GetLength(0))]);
        int r = UnityEngine.Random.Range(0,4);
        Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f*r), Vector3.one);
        map.SetTransformMatrix(pos,matrix);
        map.RefreshTile(pos);
    }

    void addRandomGrassAtBlock(int fromX, int fromY, int toX, int toY) {
        BoundsInt pos = new BoundsInt();
        pos.SetMinMax(new Vector3Int(fromX,fromY,0),new Vector3Int(toX,toY,1));

        Tile[] tA = new Tile[pos.size.x*pos.size.y];
        for (int i = 0; i < tA.Length; i++) {
            tA[i] = tiles[Chunk.maxTempLevel+1,UnityEngine.Random.Range(0,tiles.GetLength(1))];
        }

        map.SetTilesBlock(pos,tA);

        foreach (var p in pos.allPositionsWithin) {
            int r = UnityEngine.Random.Range(0,4);
            Matrix4x4 matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f*r), Vector3.one);
            map.SetTransformMatrix(p,matrix);
            map.RefreshTile(p);
        }
    }

    void addRandomGrassAtChunk(BoundsInt pos) {
        Tile[] tA = new Tile[pos.size.x*pos.size.y];
        for (int i = 0; i < tA.Length; i++) {
            tA[i] = tiles[Chunk.maxTempLevel+1,UnityEngine.Random.Range(0,tiles.GetLength(1))];
        }

        map.SetTilesBlock(pos,tA);

        foreach (var p in pos.allPositionsWithin) {
            int r = UnityEngine.Random.Range(0,4);
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
        if (debug_drawHumidity) opacity = 0.5f;

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

    void drawHumidity() {
        topTree.drawHumidity();
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

