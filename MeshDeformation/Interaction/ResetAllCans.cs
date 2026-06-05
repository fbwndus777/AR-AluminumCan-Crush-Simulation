using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ResetAllCans : MonoBehaviour
{
    public void ResetAll()
    {
        CanResetManager[] allCans = FindObjectsOfType<CanResetManager>();

        foreach (var can in allCans)
        {
            can.ResetCan();
        }

        Debug.Log($"[ResetAll] Reset {allCans.Length} cans!");
    }
}
