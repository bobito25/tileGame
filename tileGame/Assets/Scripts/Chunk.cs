using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : Quadtree
{
    public Chunk[] neighbours;

    bool loaded;
    public bool partiallyLoaded;
    public bool[] unloadedSides;
    List<Obstacle> obstacles;
    bool obstaclesLoaded;

    public BoundsInt[] sections;
    /*
        0 | 1 | 2
        7 | 8 | 3
        6 | 5 | 4
    */

    public int tempLevel;
    public static int minTempLevel = -2;
    public static int maxTempLevel = 2;
    public static int numTempLevels = 5;
    public bool hasTemp;
    public int tempIndex;

    public static int magicBiomeTemp = 25;

    public Chunk(Quadtree p, BoundsInt a) : base(p,a) {
        loaded = false;
        partiallyLoaded = false;
        obstaclesLoaded = false;
        neighbours = new Chunk[8];
        hasTemp = false;
        setSections(a);
    }

    void setSections(BoundsInt a) {
        sections = new BoundsInt[9];
        int b = Main.borderSize;
        int c = Main.chunkSize;
        sections[0] = new BoundsInt(new Vector3Int(a.x,a.yMax-b,0), new Vector3Int(a.x+b,a.yMax,1));
        sections[1] = new BoundsInt(new Vector3Int(a.x+b,a.yMax-b,0), new Vector3Int(a.xMax-b,a.yMax,1));
        sections[2] = new BoundsInt(new Vector3Int(a.xMax-b,a.yMax-b,0), a.max);
        sections[3] = new BoundsInt(new Vector3Int(a.xMax-b,a.y+b,0), new Vector3Int(a.xMax,a.yMax-b,1));
        sections[4] = new BoundsInt(new Vector3Int(a.xMax-b,a.y,0), new Vector3Int(a.xMax,a.y+b,1));
        sections[5] = new BoundsInt(new Vector3Int(a.x+b,a.y,0), new Vector3Int(a.xMax-b,a.y+b,1));
        sections[6] = new BoundsInt(a.min, new Vector3Int(a.x+b,a.y+b,1));
        sections[7] = new BoundsInt(new Vector3Int(a.x,a.y+b,0), new Vector3Int(a.x+b,a.yMax-b,1));
        sections[8] = new BoundsInt(new Vector3Int(a.x+b,a.y+b,0), new Vector3Int(a.xMax-b,a.yMax-b,1));
    }
    
    public new void unloadObstacles() {
        List<GameObject> toDelete = new List<GameObject>();
        foreach (Obstacle o in obstacles) {
            o.gameObject.SetActive(false);
            //Destroy(o.gameObject);
        }
        this.obstaclesLoaded = false;
    }
    
    public new void addObstacle(Vector2Int p, int t) {
        if (obstacles == null) {
            obstacles = new List<Obstacle>();
        }
        obstacles.Add(new Obstacle(p,t,null));
    }

    public void addNeighbour(Chunk c, int n) {
        if (neighbours[n] == null) neighbours[n] = c;
    }

    public new bool getLoaded() {
        return loaded;
    }

    public new List<Obstacle> getObstacles() {
        return obstacles;
    }

    public new bool getObstaclesLoaded() {
        return obstaclesLoaded;
    }

    public new void setLoaded(bool l) {
        loaded = l;
    }

    public new void setObstacles(List<Obstacle> os) {
        obstacles = os;
    }

    public new void setObstaclesLoaded(bool l) {
        obstaclesLoaded = l;
    }
}
