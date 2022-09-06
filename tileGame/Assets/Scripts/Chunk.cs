using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : Quadtree
{
    public Chunk[] neighbours;

    bool loaded;
    List<Obstacle> obstacles;
    bool obstaclesLoaded;

    public int tempLevel;
    public static int minTempLevel = -2;
    public static int maxTempLevel = 2;
    public bool hasTemp;

    public static int magicBiomeTemp = 25;

    public Chunk(Quadtree p, BoundsInt a) : base(p,a) {
        loaded = false;
        obstaclesLoaded = false;
        neighbours = new Chunk[8];
        hasTemp = false;
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
