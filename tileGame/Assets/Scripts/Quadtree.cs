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
    public bool loaded;
    public List<Obstacle> obstacles;

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
        loaded = false;
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
    }

    public void drawAllTrees() {
        this.draw();
        if (empty) return;
        foreach (Quadtree c in children) c.drawAllTrees();
    }

    void draw() {
        Main.DrawRect(new Vector3((float)this.area.x,(float)this.area.y,0), new Vector3((float)this.area.xMax,(float)this.area.yMax,0), Color.red, 100000000);
    }

    public void addObstacle(Vector2Int p, int t) {
        if (obstacles == null) {
            obstacles = new List<Obstacle>();
        }
        obstacles.Add(new Obstacle(p,t));
    }
}
