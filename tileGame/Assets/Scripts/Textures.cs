using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;

public class Textures
{
    public Texture2D[,] tileTextures;

    public Textures() {
        initTileTs();
        initObstacleTs();
        initPlayerT();
    }

    private void initTileTs() {
        int numTileGroups = Chunk.numTempLevels+1;
        int numTilesPerGroup = 5;

        tileTextures = new Texture2D[numTileGroups,numTilesPerGroup];

        byte[][] grassTBytes = 
        {
            File.ReadAllBytes("Assets/Tiles/grass1_x20.png"),
            File.ReadAllBytes("Assets/Tiles/grass2_x20.png"),
            File.ReadAllBytes("Assets/Tiles/grass3_x20.png"),
            File.ReadAllBytes("Assets/Tiles/grass4_x20.png"),
            File.ReadAllBytes("Assets/Tiles/grass5_x20.png")
        };

        byte[][] magicTBytes = 
        {
            File.ReadAllBytes("Assets/Tiles/magicBiome1_x20.png"),
            File.ReadAllBytes("Assets/Tiles/magicBiome2_x20.png"),
            File.ReadAllBytes("Assets/Tiles/magicBiome3_x20.png"),
            File.ReadAllBytes("Assets/Tiles/magicBiome4_x20.png"),
            File.ReadAllBytes("Assets/Tiles/magicBiome5_x20.png")
        };

        byte[][] iceTBytes = 
        {
            File.ReadAllBytes("Assets/Tiles/iceBiome1_x20.png"),
            File.ReadAllBytes("Assets/Tiles/iceBiome2_x20.png"),
            File.ReadAllBytes("Assets/Tiles/iceBiome3_x20.png"),
            File.ReadAllBytes("Assets/Tiles/iceBiome4_x20.png"),
            File.ReadAllBytes("Assets/Tiles/iceBiome5_x20.png")
        };

        byte[][] tundraTBytes = 
        {
            File.ReadAllBytes("Assets/Tiles/tundraBiome1_x20.png"),
            File.ReadAllBytes("Assets/Tiles/tundraBiome2_x20.png"),
            File.ReadAllBytes("Assets/Tiles/tundraBiome3_x20.png"),
            File.ReadAllBytes("Assets/Tiles/tundraBiome4_x20.png"),
            File.ReadAllBytes("Assets/Tiles/tundraBiome5_x20.png")
        };

        byte[][] savanaTBytes = 
        {
            File.ReadAllBytes("Assets/Tiles/savannaBiome1_x20.png"),
            File.ReadAllBytes("Assets/Tiles/savannaBiome2_x20.png"),
            File.ReadAllBytes("Assets/Tiles/savannaBiome3_x20.png"),
            File.ReadAllBytes("Assets/Tiles/savannaBiome4_x20.png"),
            File.ReadAllBytes("Assets/Tiles/savannaBiome5_x20.png")
        };

        byte[][] desertTBytes = 
        {
            File.ReadAllBytes("Assets/Tiles/desertBiome1_x20.png"),
            File.ReadAllBytes("Assets/Tiles/desertBiome2_x20.png"),
            File.ReadAllBytes("Assets/Tiles/desertBiome3_x20.png"),
            File.ReadAllBytes("Assets/Tiles/desertBiome4_x20.png"),
            File.ReadAllBytes("Assets/Tiles/desertBiome5_x20.png")
        };
        
        byte[][][] Tbytes = {
            magicTBytes,
            iceTBytes,
            tundraTBytes,
            grassTBytes,
            savanaTBytes,
            desertTBytes
        };

        for (int i = 0; i < numTileGroups; i++) {
            for (int j = 0; j < numTilesPerGroup; j++) {
                tileTextures[i,j] = new Texture2D(20,20);
                tileTextures[i,j].LoadImage(Tbytes[i][j]);
                tileTextures[i,j].filterMode = FilterMode.Point;
                tileTextures[i,j].wrapMode = TextureWrapMode.Clamp;
            }
        }
    }

    private void initObstacleTs() {

    }

    private void initPlayerT() {

    }
}
