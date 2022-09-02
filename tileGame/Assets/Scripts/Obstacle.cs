using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Obstacle
{
    public Vector2Int pos;
    public int type;
    public GameObject gameObject;

    public Obstacle(Vector2Int p, int t, GameObject g) {
        pos = p;
        type = t;
        gameObject = g;
    }
}
