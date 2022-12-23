using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Biome
{
    /*
    Biome List

    -2: polar desert, ice sheet
    -1: tundra; taiga/boreal forest
    0: grassy plains, temperate forest
    1: grass savanna, tree savanna, (semi arid desert, tropical rainforest)
    2: sand desert, stone desert

    */

    public static int[] numBiomesPerTemp = {2,2,2,2,2};

    static List<List<int>>[] obstacleIndexsPerBiomePerTemp;
    static List<List<float>>[] obstacleProbPerBiomePerTemp;
    public static List<int>[] obstacleNumPerBiomePerTemp;

    public static int humidityLevel = 3;

    public int tempI;
    public int biomeNum;

    public Biome(int t, int n) {
        tempI = t;
        biomeNum = n;
    }

    public static void initBiomes() {
        List<List<int>> iceOIs = new List<List<int>>(){new List<int>(),new List<int>()};
        List<List<int>> tundraOIs = new List<List<int>>(){new List<int>(){0},new List<int>(){0,1}};
        List<List<int>> grassOIs = new List<List<int>>(){new List<int>(){0,1},new List<int>(){0,1}};
        List<List<int>> savanaOIs = new List<List<int>>(){new List<int>(){1},new List<int>(){1}};
        List<List<int>> desertOIs = new List<List<int>>(){new List<int>(),new List<int>(){1}};
        List<List<int>>[] temp = {
            iceOIs,
            tundraOIs,
            grassOIs,
            savanaOIs,
            desertOIs
        };
        obstacleIndexsPerBiomePerTemp = temp;

        List<List<float>> iceOPs = new List<List<float>>(){new List<float>(),new List<float>()};
        List<List<float>> tundraOPs = new List<List<float>>(){new List<float>(){1f},new List<float>(){1f}};
        List<List<float>> grassOPs = new List<List<float>>(){new List<float>(){0.5f,0.5f},new List<float>(){0.5f,0.5f}};
        List<List<float>> savanaOPs = new List<List<float>>(){new List<float>(){1f},new List<float>(){1f}};
        List<List<float>> desertOPs = new List<List<float>>(){new List<float>(),new List<float>(){1f}};
        List<List<float>>[] temp2 = {
            iceOPs,
            tundraOPs,
            grassOPs,
            savanaOPs,
            desertOPs
        };
        obstacleProbPerBiomePerTemp = temp2;

        int obstaclesPerTile = Main.obstaclesPerTile;
        List<int> iceONs = new List<int>(){0,0};
        List<int> tundraONs = new List<int>(){obstaclesPerTile/8,obstaclesPerTile};
        List<int> grassONs = new List<int>(){obstaclesPerTile/8,obstaclesPerTile};
        List<int> savanaONs = new List<int>(){obstaclesPerTile/8,obstaclesPerTile/4};
        List<int> desertONs = new List<int>(){0,obstaclesPerTile/16};
        List<int>[] temp3 = {
            iceONs,
            tundraONs,
            grassONs,
            savanaONs,
            desertONs
        };
        obstacleNumPerBiomePerTemp = temp3;
    }

    public int getRandomPossibleObstacleIndex() {
        float r = Random.value;
        int l = obstacleProbPerBiomePerTemp[tempI-1][biomeNum].Count;
        for (int i = 0; i < l; i++) {
            r -= obstacleProbPerBiomePerTemp[tempI-1][biomeNum][i];
            if (r < 0) return i;
        }
        return l-1;
    }
}
