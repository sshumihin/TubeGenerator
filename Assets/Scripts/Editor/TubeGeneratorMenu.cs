using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TubeGeneratorMenu 
{
    [MenuItem("GameObject/NumberValley/TubeGenerator")]
    static void CreateTubeGenerator()
    {
        GameObject prefab = Resources.Load<GameObject>("TubeGenerator");
        GameObject go = GameObject.Instantiate(prefab);
        TubeGenerator tube = go.GetComponent<TubeGenerator>();
        if (tube)
        {
            tube.CreateTubeGameObject();
            Selection.activeObject = go;
        }
    }

}
