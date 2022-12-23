using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : Quadtree
{
    public Chunk[] neighbours;

    public Vector2Int pos;
    public bool hasPos;

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

    public Biome biome;

    public Chunk(Quadtree p, BoundsInt a) : base(p,a) {
        loaded = false;
        partiallyLoaded = false;
        obstaclesLoaded = false;
        neighbours = new Chunk[8];
        hasTemp = false;
        hasPos = false;
        setSections(a);
    }

    void setSections(BoundsInt a) {
        
        sections = new BoundsInt[9];
        for (int i = 0; i < 9; i++) sections[i] = new BoundsInt();
        int b = Main.borderSize;
        sections[0].SetMinMax(new Vector3Int(a.x,a.yMax-b,0), new Vector3Int(a.x+b,a.yMax,1));
        sections[1].SetMinMax(new Vector3Int(a.x+b,a.yMax-b,0), new Vector3Int(a.xMax-b,a.yMax,1));
        sections[2].SetMinMax(new Vector3Int(a.xMax-b,a.yMax-b,0), a.max);
        sections[3].SetMinMax(new Vector3Int(a.xMax-b,a.y+b,0), new Vector3Int(a.xMax,a.yMax-b,1));
        sections[4].SetMinMax(new Vector3Int(a.xMax-b,a.y,0), new Vector3Int(a.xMax,a.y+b,1));
        sections[5].SetMinMax(new Vector3Int(a.x+b,a.y,0), new Vector3Int(a.xMax-b,a.y+b,1));
        sections[6].SetMinMax(a.min, new Vector3Int(a.x+b,a.y+b,1));
        sections[7].SetMinMax(new Vector3Int(a.x,a.y+b,0), new Vector3Int(a.x+b,a.yMax-b,1));
        sections[8].SetMinMax(new Vector3Int(a.x+b,a.y+b,0), new Vector3Int(a.xMax-b,a.yMax-b,1));
        
        /* alternative (better?)
        sections = new BoundsInt[9];
        int b = Main.borderSize;
        int c = Main.chunkSize;
        int l = c - (b*2);
        sections[0] = new BoundsInt(new Vector3Int(a.x,a.yMax-b,0), new Vector3Int(b,b,1));
        sections[1] = new BoundsInt(new Vector3Int(a.x+b,a.yMax-b,0), new Vector3Int(l,b,1));
        sections[2] = new BoundsInt(new Vector3Int(a.xMax-b,a.yMax-b,0), new Vector3Int(b,b,1));
        sections[3] = new BoundsInt(new Vector3Int(a.xMax-b,a.y+b,0), new Vector3Int(b,l,1));
        sections[4] = new BoundsInt(new Vector3Int(a.xMax-b,a.y,0), new Vector3Int(b,b,1));
        sections[5] = new BoundsInt(new Vector3Int(a.x+b,a.y,0), new Vector3Int(l,b,1));
        sections[6] = new BoundsInt(a.min, new Vector3Int(b,b,1));
        sections[7] = new BoundsInt(new Vector3Int(a.x,a.y+b,0), new Vector3Int(b,l,1));
        sections[8] = new BoundsInt(new Vector3Int(a.x+b,a.y+b,0), new Vector3Int(l,l,1));
        */
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

    public int checkNeighbourTempIndex(int s) { // s from 0 to 3 (top,right,bottom,left)
        int n = 1 + (2*s);
        if (neighbours[n] != null && neighbours[n].hasTemp) {
            return neighbours[n].tempIndex;
        } else {
            Debug.Log("e: neighbour for tempIndex should be preloaded but is null or missing temp - effected sections will be reloaded when possible (checkNeighbourTempIndex in Chunk)");
            if (!partiallyLoaded || !unloadedSides[s]) {
                partiallyLoaded = true;
                if (unloadedSides == null) unloadedSides = new bool[4];
                unloadedSides[s] = true;
            }
            return tempIndex;
        }
    }

    public void checkDoneLoading() {
        foreach (bool b in unloadedSides) if (b) return;
        partiallyLoaded = false;
    }
}
