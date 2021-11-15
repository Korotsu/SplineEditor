using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.UI;
using System;

public class SplineManager : MonoBehaviour
{
    [SerializeField] private GameObject ptsListParent;
    [SerializeField] private GameObject ptPrefab;
    [SerializeField] private Dropdown   typeSelectorDropdown;
    [SerializeField] private GameObject animatedObject;

    //[SerializeField] private GameObject cam;
    //[SerializeField] private float camSpeed;

    static public List<Transform> ptsList = new List<Transform>();
    private LineRenderer lineRenderer;
    private Curve curve = new Curve();

    [Serializable]
    struct SerializableVector3
    {
        float _x;
        float _y;
        float _z;

        public SerializableVector3(Vector3 vect)
        {
            _x = vect.x;
            _y = vect.y;
            _z = vect.z;
        }
        public Vector3 ToVector3()
        {
            return new Vector3(_x, _y, _z);
        }
    }

    [Serializable]
    class Curve
    {
        public CurvesTypes type = CurvesTypes.Bezier;
        public SerializableVector3[] controlPtsList = new SerializableVector3[4];
    }

    private List<Vector3> curvePtsList = new List<Vector3>();
    private List<Vector3> curveDerviativePtsList = new List<Vector3>();
    private string filename = "";
    private double animationSpeed = 0.0f;

    private int shouldCloseTheBSpline = 0;
    static public bool initialized = false;
    static public bool shouldUpdate = false;

    private bool animPlaying = false;

    delegate Vector3 DelegateType(float t);

    [Serializable]
    enum CurvesTypes : int
    {
        Hermite = 0,
        Bezier = 1,
        B_Spline = 2,
        Catmull_Rom = 3
    };

    // Start is called before the first frame update
    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();

        UpdateCurve();
        initialized = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (UpdatePointsList() || shouldUpdate)
        {
            UpdateCurve();
            shouldUpdate = false;
        }

        if (transform.childCount == 0 && animPlaying)
        {
            animPlaying = false;
            typeSelectorDropdown.enabled = true;
        }

        /*if (Input.GetButton("Move"))
        {
            UpdateMovement();
        }*/
    }

    void UpdateCurve()
    {
        switch (curve.type)
        {
            case CurvesTypes.Hermite:
                UpdateHermiteCurve();
                break;
            case CurvesTypes.Bezier:
                UpdateBezierCurve();
                break;
            case CurvesTypes.B_Spline:
                UpdateBSplineCurve();
                break;
            case CurvesTypes.Catmull_Rom:
                UpdateCatmullRomCurve();
                break;
            default:
                break;
        }
    }

    void UpdateBezierCurve()
    {
        curvePtsList.Clear();
        lineRenderer.positionCount = curvePtsList.Count;

        lineRenderer.SetPositions(curvePtsList.ToArray());

        int i = 0;
        Vector3 joncVect = new Vector3();

        while (i < ((ptsList.Count - 1) / 3))
        {
            if (joncVect.magnitude > 0.001f)
            {
                ptsList[1 + (i * 3)].position = ptsList[0 + (i * 3)].position + (ptsList[0 + (i * 3)].position - joncVect); 
            }
            float t = 0;
            while (t <= 1.0f)
            {
                DelegateType S = t => (1 - 3*t + 3*t*t - t*t*t) * ptsList[0 + (i * 3)].position + 3 * t * (1 - 2*t + t*t) * ptsList[1 + (i * 3)].position + 3 * t * t * (1 - t) * ptsList[2 + (i * 3)].position + t * t * t * ptsList[3 + (i * 3)].position;
                curvePtsList.Add(S(t));

                t += 0.001f;
            }

            joncVect = ptsList[2 + (i * 3)].position;

            i++;
        }

        lineRenderer.positionCount = curvePtsList.Count;

        lineRenderer.SetPositions(curvePtsList.ToArray());
    }

    void UpdateHermiteCurve()
    {
        curvePtsList.Clear();
        lineRenderer.positionCount = curvePtsList.Count;

        lineRenderer.SetPositions(curvePtsList.ToArray());

        if (ptsList.Count >= 4)
        {
            int i = 0;

            while (i < ((ptsList.Count - 1) / 3))
            {
                float t = 0;

                while (t <= 1.0f)
                {
                    DelegateType S = t => (2 * t * t * t - 3 * t * t + 1) * ptsList[0 + (i * 3)].position + (-2 * t * t * t + 3 * t * t) * ptsList[3 + (i * 3)].position + (t * t * t - 2 * t * t + t) * ptsList[1 + (i * 3)].position + (t * t * t - t * t) * ptsList[2 + (i * 3)].position;
                    curvePtsList.Add(S(t));

                    t += 0.001f;
                }

                i++;
            }
        }

        lineRenderer.positionCount = curvePtsList.Count;

        lineRenderer.SetPositions(curvePtsList.ToArray());
    }

    public void ChangeType(int newType)
    {
        curve.type = (CurvesTypes)(newType);
        UpdateCurve();
    }

    public void SaveCurve()
    {
        curve.controlPtsList = new SerializableVector3[ptsList.Count];

        for (int i = 0; i < ptsList.Count; i++)
        {
            curve.controlPtsList[i] = new SerializableVector3(ptsList[i].position);
        }

        string destination = Application.streamingAssetsPath + "/save_" + filename + ".dat";

        FileStream file;

        if (File.Exists(destination)) file = File.OpenWrite(destination);
        else file = File.Create(destination);

        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(file, curve);
        file.Close();
    }

    public void LoadCurve()
    {
        if (!animPlaying)
        {

            string destination = Application.streamingAssetsPath + "/save_" + filename + ".dat";
            FileStream file;

            if (File.Exists(destination)) file = File.OpenRead(destination);
            else
            {
                Debug.LogError("File : " + destination + "; not found");
                return;
            }

            BinaryFormatter bf = new BinaryFormatter();
            curve = (Curve)bf.Deserialize(file);
            file.Close();
        }

        typeSelectorDropdown.value = ((int)curve.type);

        LoadPts();

        UpdateCurve();
    }

    public void ChangeFileName(string newFileName)
    {
        filename = newFileName;
    }

    private void UpdateBSplineCurve()
    {
        curvePtsList.Clear();
        lineRenderer.positionCount = curvePtsList.Count;

        lineRenderer.SetPositions(curvePtsList.ToArray());

        List<Vector3> tempControlPtsList = new List<Vector3>();
        for (int i = 0; i < ptsList.Count; i++)
        {
            tempControlPtsList.Add(ptsList[i].position);
        }

        int j = 0;

        while (j < tempControlPtsList.Count - shouldCloseTheBSpline && tempControlPtsList.Count >= 4)
        {
            float t = 0.0f;
            int i = 0;

            while (t <= 1.0f)
            {
                DelegateType S = t => (1.0f / 6.0f) * ((-t * t * t + 3.0f * t * t - 3.0f * t + 1.0f) * tempControlPtsList[0] + (3.0f * t * t * t - 6.0f * t * t + 4.0f) * tempControlPtsList[1] + (-3.0f * t * t * t + 3.0f * t * t + 3.0f * t + 1.0f) * tempControlPtsList[2] + t * t * t * tempControlPtsList[3]);
                curvePtsList.Add(S(t));

                t += 0.001f;
                i++;
            }

            Vector3 tempVec = tempControlPtsList[0];
            tempControlPtsList.RemoveAt(0);
            tempControlPtsList.Add(tempVec);

            j++;
        }

        lineRenderer.positionCount = curvePtsList.Count;

        lineRenderer.SetPositions(curvePtsList.ToArray());
    }

    private void UpdateCatmullRomCurve()
    {

        curvePtsList.Clear();
        lineRenderer.positionCount = curvePtsList.Count;

        lineRenderer.SetPositions(curvePtsList.ToArray());

        if (ptsList.Count >= 4)
        {
            for (int i = 0; i < ptsList.Count - 3; i++)
            {
                float t = 0;

                while (t <= 1.0f)
                {
                    DelegateType S = t => (1.0f / 2.0f) * ((-t * t * t + 2 * t * t - t) * ptsList[0 + i].position + (3 * t * t * t - 5 * t * t + 2) * ptsList[1 + i].position + (-3 * t * t * t + 4 * t * t + t) * ptsList[2 + i].position + (t * t * t - t * t) * ptsList[3 + i].position);
                    curvePtsList.Add(S(t));

                    t += 0.001f;
                }
            }
        }

        lineRenderer.positionCount = curvePtsList.Count;

        lineRenderer.SetPositions(curvePtsList.ToArray());
    }

    public void ChangeShouldCloseBSplineValue(bool newValue)
    {
        shouldCloseTheBSpline = newValue ? 0 : 1;
        UpdateCurve();
    }

    /*private void UpdateMovement()
    {
        Vector3 movement = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), Input.GetAxis("Forward"));
        movement *= camSpeed;

        //cam.transform.Translate()
    }*/

    private bool UpdatePointsList()
    {
        foreach (Transform pt in ptsList)
        {
            if (pt.hasChanged)
            {
                return true;
            }
        }

        return false;
    }

    /*private void GetAllPts()
    {
        ptsList.Clear();

        for (int i = 0; i < ptsListParent.transform.childCount; i++)
        {
            ptsList.Add(ptsListParent.transform.GetChild(i));
        }
    }*/

    private void LoadPts()
    {
        int i = 0;
        while (i < curve.controlPtsList.Length)
        {
            if (i < ptsList.Count)
            {
                ptsList[i].position = curve.controlPtsList[i].ToVector3();
            }

            else
            {
                GameObject newPt = Instantiate<GameObject>(ptPrefab, ptsListParent.transform);
                newPt.transform.position = curve.controlPtsList[i].ToVector3();
            }

            i++;
        }

        while (i < ptsList.Count)
        {
            Destroy(ptsList[ptsList.Count - 1]);
        }
    }

    public void ChangeAnimationSpeed(string newSpeed)
    {
        newSpeed = newSpeed.Replace('.', ',');
        if(double.TryParse(newSpeed, out double newAnimSpeed))
        {
            animationSpeed = System.Math.Round(newAnimSpeed, 3);
        }
    }

    public void StartAnimationCoroutine()
    {
        StartCoroutine("PlayAnimation");
    }

    public IEnumerator PlayAnimation()
    {
        GameObject animObject = Instantiate<GameObject>(animatedObject, this.transform);
        int stepLenght  = (int)(animationSpeed / 0.001f);
        int nbStep      = curvePtsList.Count / stepLenght;

        animPlaying = true;
        typeSelectorDropdown.enabled = false;

        for (int i = 0; i < nbStep; i++)
        {
            animObject.transform.position = curvePtsList[stepLenght * i];
            if (curvePtsList.Count > stepLenght * (i + 1))
            {
                animObject.transform.forward = curvePtsList[(stepLenght * (i + 1))] - animObject.transform.position;
            }
            yield return new WaitForSeconds(0.01f);
        }

        Destroy(animObject);
        yield return null;
    }
}
