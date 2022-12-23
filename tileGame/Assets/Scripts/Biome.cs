using System.Collections;
using System.Collections.Generic;

public class Biome
{
    /*
    Biome List

    -2: polar desert, ice sheet
    -1: tundra; taiga/boreal forest
    0: grassy plains, temperate forest
    1: grass savanna, tree savanna, semi arid desert, tropical rainforest
    2: sand desert, stone desert

    */

    public static int[] numBiomesPerTemp = {2,2,2,4,2};

    public int tempI;
    public int biomeNum;

    public Biome(int t, int n) {
        tempI = t;
        biomeNum = n;
    }

    public void initBiomes() {
        
    }
}
