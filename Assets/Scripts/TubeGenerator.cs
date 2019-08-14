using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TubeGenerator : MonoBehaviour
{
    public List<Transform> points = new List<Transform>();

    public List<TubeObject> lods = new List<TubeObject>();

    [SerializeField] private eParameterMode ParameterMode;

    [SerializeField] private int sides = 3;

    [SerializeField] private float radius = 1f;

    [SerializeField] private int subdivision = 1;

    [SerializeField] private int triangles = 1000;

    [SerializeField] private int numberLODs = 0;

    [SerializeField] private float koeffLOD = 1f;

    [SerializeField] private eSplineMode SplineMode;

    [SerializeField] private bool hasThickness = false;

    [SerializeField] private float thickness = 0.1f;

    [SerializeField] private eRadius shellType;


    private TubeSplines m_splines = new TubeSplines();

    private Transform m_rootPoints;

    private Transform m_rootLODs;

    private int m_polycountMin;

    private TubeObject m_tube;


    [Serializable]
    public class TubeObject
    {
        public GameObject go;

        public int TrianglesCount
        {
            get
            {
                if (mesh == null) return 0;

                return mesh.triangles.Length / 3;
            }
        }

        private Mesh mesh;

        private MeshFilter filter;

        public void SetTube(Transform parent)
        {
            GameObject prefab = Resources.Load<GameObject>("TubeObject");
            go = GameObject.Instantiate(prefab);
            go.transform.SetParent(parent);

            mesh = go.GetComponent<Mesh>();

            filter = go.GetComponent<MeshFilter>();
        }

        public void WriteMesh(MeshBuilder.CustomMesh cmesh)
        {
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "tube";
            }

            cmesh.WriteMesh(ref mesh);

            mesh.RecalculateNormals();

            MeshUtility.Optimize(mesh);

            filter.sharedMesh = mesh;
        }
    }

    public class TubeSettings
    {
        public int sides;

        public float radius;

        public bool hasThickness;

        public float thickness;

        public eRadius shellType;

        public List<TubePoint> samples;

    }

    public enum eRadius
    {
        OUTWARD,
        INWARD
    }

    public enum eSplineMode
    {
        Linear,
        Spline
    }

    public enum eParameterMode
    {
        ByParameters = 0,
        ByPolyCount = 1
    }

    public class LODInfo
    {
        public string name;

        public int trisCount;
    }

    public List<LODInfo> GetLODInfo()
    {
        List<LODInfo> list = new List<LODInfo>();
        for (int i = 0; i < lods.Count; i++)
        {
            list.Add(new LODInfo() { name = lods[i].go.name, trisCount = lods[i].TrianglesCount });
        }

        return list;
    }

    public int GetSamplesCount()
    {
        if (m_splines == null) return -1;

        return m_splines.Samples.Count;
    }

    public void CreateTubeGameObject()
    {
        points.Clear();

        m_rootPoints = this.transform.Find("points");

        m_rootLODs = this.transform.Find("LODs");

        for (int i = 0; i < 2; i++)
        {
            Vector3 pos = Vector3.zero + Vector3.forward * 10f * i;
            AddTubePoint(pos);
        }

        m_tube = new TubeObject();
        m_tube.SetTube(this.transform);

        RebuildMesh();
    }

    public void AddTubePoint(Vector3 pos)
    {
        int idx = points.Count;
        GameObject go = new GameObject("point0" + idx);
        go.transform.position = pos;
        go.transform.SetParent(m_rootPoints);

        Undo.RegisterCreatedObjectUndo(go, "Add Point");

        points.Add(go.transform);
    }

    public void RemoveLastPoint()
    {
        if (points.Count <= 2) return;

        Transform tp = points[points.Count - 1];
        points.RemoveAt(points.Count - 1);

        DestroyImmediate(tp.gameObject);

        RebuildMesh();
    }

    public void BuildLODs()
    {
        ClearLODs();

        if (numberLODs == 0) return;

        if (m_rootLODs == null)
        {
            m_rootLODs = this.transform.Find("LODs");
        }

        int sidesPrev = sides;
        for (int i = 0; i < numberLODs; i++)
        {
            float koeff = koeffLOD * (i + 1);
            int tris = (int)((float)triangles / koeff);
            int newsides = CalculateParametersByPolycount(tris, hasThickness, m_splines.Samples.Count);
            if (newsides < sidesPrev)
            {
                TubeSettings settings = new TubeSettings()
                {
                    hasThickness = hasThickness,
                    radius = radius,
                    samples = m_splines.Samples,
                    shellType = shellType,
                    sides = newsides,
                    thickness = thickness
                };
                MeshBuilder builder = new MeshBuilder(settings);
                MeshBuilder.CustomMesh mesh = builder.Build();

                TubeObject lod = new TubeObject();
                lod.SetTube(this.m_rootLODs);
                lod.WriteMesh(mesh);

                lod.go.name = "lod" + (i + 1);

                lods.Add(lod);

                sidesPrev = newsides;
            }
            else
            {
                Debug.LogWarning("You are reached a triangles limit!");
            }
        }
    }

    public void ClearLODs()
    {
        for (int i = 0; i < lods.Count; i++)
        {
            if (lods[i].go != null)
            {
                DestroyImmediate(lods[i].go);
            }

            lods[i] = null;
        }

        lods.Clear();
    }

    public void SetTrianglesCount(int tris)
    {
        triangles = tris;
    }

    [ContextMenu("Rebuild Mesh")]
    public void RebuildMesh()
    {
        if (points.Count < 2) return;

        CheckParametersBound();

        EvaluateSpline();

        if (m_splines.Samples.Count < 2) return;

        if (ParameterMode == eParameterMode.ByPolyCount)
        {
            sides = CalculateParametersByPolycount(triangles, hasThickness, m_splines.Samples.Count);
        }
        else
        {
            triangles = CalculateTrianglesCount(sides, hasThickness, m_splines.Samples.Count);
        }

        TubeSettings settings = new TubeSettings()
        {
            hasThickness = hasThickness,
            radius = radius,
            samples = m_splines.Samples,
            shellType = shellType,
            sides = sides,
            thickness = thickness
        };
        MeshBuilder builder = new MeshBuilder(settings);
        MeshBuilder.CustomMesh mesh = builder.Build();

        WriteMesh(mesh);
    }

    private int CalculateParametersByPolycount(int _tris, bool _hasThickness, int _samplesCount)
    {
        int _sides = _tris / 2 / (_samplesCount - 1);
        if (_hasThickness)
        {
            _sides /= 2;
            int capTris = _sides * 2 * 2;
            while (capTris > _tris - _sides * 2 * 2)
            {
                _sides--;
                capTris = _sides * 2 * 2;
            }
        }

        if (_sides < 3) _sides = 3;

        return _sides;
    }

    private int CalculateTrianglesCount(int _sides, bool _hasThickness, int _samplesCount)
    {
        int tris = _sides * 2 * (_samplesCount - 1);

        if (_hasThickness)
        {
            tris *= 2;
            int captris = _sides * 2 * 2;

            tris += captris;
        }

        return tris;
    }

    private int CalculateTrianglesCountMinimum()
    {
        int _sides = 3;
        int tris = _sides * 2 * (m_splines.Samples.Count - 1);

        if (hasThickness)
        {
            tris *= 2;
            int captris = _sides * 2 * 2;

            tris += captris;
        }

        return tris;
    }

    private void CheckParametersBound()
    {
        if (sides < 3) sides = 3;

        if (subdivision < 1) subdivision = 1;

        if (radius < 0.1f) radius = 0.1f;

        if (thickness < 0f) thickness = 0f;

        if (shellType == eRadius.INWARD)
        {
            if (thickness >= radius - 0.1f) thickness = radius - 0.1f;
        }

        if (triangles < 6) triangles = 6;

        if (numberLODs < 0) numberLODs = 0;
        if (numberLODs > 5) numberLODs = 5;

        if (koeffLOD < 1f) koeffLOD = 1f;
    }

    private void EvaluateSpline()
    {
        m_splines.CalculateSpline(points, SplineMode, subdivision);
    }

    void WriteMesh(MeshBuilder.CustomMesh cmesh)
    {
        m_tube.WriteMesh(cmesh);
    }

    private void OnDrawGizmos()
    {
        for (int i = 0; i < m_splines.Samples.Count; i++)
        {
            Vector3 pos = m_splines.Samples[i].position;
            if (i > 1)
            {
                Vector3 prev = m_splines.Samples[i - 1].position;
                Gizmos.DrawLine(prev, pos);
            }

            Gizmos.DrawSphere(pos, 0.5f);
        }
    }
}
