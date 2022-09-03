using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Quadtree
{
    public Quadtree parent;
    public Quadtree[] children;
    /*
        0  |  1
       ---------
        3  |  2
    */
    public BoundsInt area;
    public int level;
    public bool empty;

    public int tempOffset; // -1,0,1

    public Quadtree(Quadtree p, BoundsInt a) {
        parent = p;
        children = new Quadtree[4];
        area = a;
        if (p != null) {
            level = p.level - 1;
        } else {
            level = Main.quadtreeMaxLevel;
        }
        empty = true;
    }

    public Quadtree getTreeFromPos(Vector3Int p) {
        if (this.level > 1) {
            if (this.empty) {
                this.split();
            }
            foreach (Quadtree q in children) {
                if (q.area.Contains(p)) {
                    return q.getTreeFromPos(p);
                }
            }
            Debug.Log("e: pos not in children (getChunkFromPos in Quadtree)");
            return null;
        } else {
            return this;
        }
    }

    public void split() {
        if (this.level > 2) {
            BoundsInt bi1 = new BoundsInt();
            bi1.SetMinMax(new Vector3Int(area.x,(int)area.center.y,0), new Vector3Int((int)area.center.x,area.yMax,1));
            children[0] = new Quadtree(this, bi1);
            BoundsInt bi2 = new BoundsInt();
            bi2.SetMinMax(new Vector3Int((int)area.center.x,(int)area.center.y,0), area.max);
            children[1] = new Quadtree(this, bi2);
            BoundsInt bi3 = new BoundsInt();
            bi3.SetMinMax(new Vector3Int((int)area.center.x,area.y,0), new Vector3Int(area.xMax,(int)area.center.y,1));
            children[2] = new Quadtree(this, bi3);
            BoundsInt bi4 = new BoundsInt();
            bi4.SetMinMax(area.min, new Vector3Int((int)area.center.x,(int)area.center.y,1));
            children[3] = new Quadtree(this, bi4);
            this.empty = false;
        } else {
            BoundsInt bi1 = new BoundsInt();
            bi1.SetMinMax(new Vector3Int(area.x,(int)area.center.y,0), new Vector3Int((int)area.center.x,area.yMax,1));
            children[0] = new Chunk(this, bi1);
            BoundsInt bi2 = new BoundsInt();
            bi2.SetMinMax(new Vector3Int((int)area.center.x,(int)area.center.y,0), area.max);
            children[1] = new Chunk(this, bi2);
            BoundsInt bi3 = new BoundsInt();
            bi3.SetMinMax(new Vector3Int((int)area.center.x,area.y,0), new Vector3Int(area.xMax,(int)area.center.y,1));
            children[2] = new Chunk(this, bi3);
            BoundsInt bi4 = new BoundsInt();
            bi4.SetMinMax(area.min, new Vector3Int((int)area.center.x,(int)area.center.y,1));
            children[3] = new Chunk(this, bi4);
            this.empty = false;
        }
        
    }

    public void drawAllTrees() {
        this.draw();
        if (empty) return;
        foreach (Quadtree c in children) c.drawAllTrees();
    }

    void draw() {
        Main.DrawRect(new Vector3((float)this.area.x,(float)this.area.y,0), new Vector3((float)this.area.xMax,(float)this.area.yMax,0), Color.red, 100000000);
    }
    
    public void unloadObstacles() {
        Debug.Log("e: tried to use unloadObstacles func of Quadtree (unloadObstacles in Quadtree)");
    }
    
    public void addObstacle(Vector2Int p, int t) {
        Debug.Log("e: tried to use addObstacle func of Quadtree (addObstacle in Quadtree)");
    }
    
    public bool getLoaded() {
        Debug.Log("e: tried to access loaded var of Quadtree (getLoaded in Quadtree)");
        return false;
    }

    public List<Obstacle> getObstacles() {
        Debug.Log("e: tried to access obstacles var of Quadtree (getObstacles in Quadtree)");
        return null;//new List<Obstacle>();
    }

    public bool getObstaclesLoaded() {
        Debug.Log("e: tried to access obstaclesLoaded var of Quadtree (getObstaclesLoaded in Quadtree)");
        return false;
    }

    public void setLoaded(bool l) {
        Debug.Log("e: tried to access loaded var of Quadtree (setLoaded in Quadtree)");
    }

    public void setObstacles(List<Obstacle> os) {
        Debug.Log("e: tried to access obstacles var of Quadtree (setObstacles in Quadtree)");
    }

    public void setObstaclesLoaded(bool l) {
        Debug.Log("e: tried to access obstaclesLoaded var of Quadtree (setObstaclesLoaded in Quadtree)");
    }
}
