using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Loadable
{
    public Tile[] tiles;
    public BoundsInt area;

    public Loadable(Tile[] tA, BoundsInt a) {
        tiles = tA;
        area = a;
    }

    public void load() {
        Main.map.SetTilesBlock(area,tiles);
    }
}
