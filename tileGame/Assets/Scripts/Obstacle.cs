using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Obstacle
{
    public Vector2Int pos;
    public int type;

    public Obstacle(Vector2Int p, int t) {
        pos = p;
        type = t;
    }
}
