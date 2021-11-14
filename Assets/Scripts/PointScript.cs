using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        SplineManager.ptsList.Add(this.transform);
        if (SplineManager.initialized)
        {
            SplineManager.shouldUpdate = true;
        }
    }

    private void OnDestroy()
    {
        SplineManager.ptsList.Remove(this.transform);
        SplineManager.shouldUpdate = true;
    }
}
