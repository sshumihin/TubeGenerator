using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshBuilder
{
    [Serializable]
    public class CustomMesh
    {
        public int vertexCount
        {
            get { return vertices.Length; }
            set { }
        }

        public Vector3[] vertices = new Vector3[0];

        public Vector3[] normals = new Vector3[0];

        public Vector2[] uv = new Vector2[0];

        public int[] triangles = new int[0];

        public void WriteMesh(ref Mesh input)
        {
            if (input == null) input = new Mesh();
            if (vertices == null || vertices.Length <= 65000)
            {
                input.Clear();
                input.vertices = vertices;
                input.normals = normals;
                if (uv.Length == vertices.Length) input.uv = uv;
                input.triangles = triangles;
                input.RecalculateBounds();
            }
        }
    }

    private CustomMesh m_customMesh = new CustomMesh();

    private int m_bodyVertexCount = 0;

    private int m_bodyTrisCount = 0;

    private int m_capVertexCount = 0;

    private int m_capTrisCount = 0;

    private Vector2 m_uvs = Vector2.zero;

    private int m_sides = 3;

    private float m_radius = 1f;

    private bool m_hasThickness = false;

    private float m_thickness = 0.1f;

    private TubeGenerator.eRadius m_shellType;

    private List<TubePoint> m_samples;


    public MeshBuilder(TubeGenerator.TubeSettings settings)
    {
        m_radius = settings.radius;
        m_sides = settings.sides;
        m_samples = settings.samples;
        m_hasThickness = settings.hasThickness;
        m_thickness = settings.thickness;
        m_shellType = settings.shellType;
    }

    public CustomMesh Build()
    {
        CalculateTriangleComponents();

        AllocateMesh(m_bodyVertexCount, m_bodyTrisCount);

        int vindex = 0;
        GenerateVertices(ref vindex, true);
        if (m_hasThickness) GenerateVertices(ref vindex, false);

        GenerateBodyPlaneTriangles();

        if (m_hasThickness)
        {
            MakeInnerSide(m_customMesh);

            GenerateCaps();
        }

        return m_customMesh;
    }

    private void CalculateTriangleComponents()
    {
        m_bodyVertexCount = (m_sides + 1) * m_samples.Count;
        m_bodyTrisCount = m_sides * (m_samples.Count - 1) * 2 * 3;

        if (m_hasThickness)
        {
            m_bodyVertexCount *= 2;
            m_bodyTrisCount *= 2;
        }
    }

    void AllocateMesh(int vertexCount, int trisCount)
    {
        if (m_customMesh.vertexCount != vertexCount)
        {
            m_customMesh.vertices = new Vector3[vertexCount];
            m_customMesh.normals = new Vector3[vertexCount];
            m_customMesh.uv = new Vector2[vertexCount];
        }

        if (m_customMesh.triangles.Length != trisCount)
        {
            m_customMesh.triangles = new int[trisCount];
        }
    }

    void GenerateVertices(ref int vertexIndex, bool isOuterWall)
    {
        for (int i = 0; i < m_samples.Count; i++)
        {
            Vector3 center = m_samples[i].position;
            Vector3 right = m_samples[i].right;

            float uvPercent = (float)(i) / m_samples.Count;
            for (int n = 0; n < m_sides + 1; n++)
            {
                float anglePercent = (float)(n) / m_sides;
                Quaternion rot = Quaternion.AngleAxis(anglePercent * 360f, m_samples[i].direction);

                float offset = GetVertexThicknessOffset(isOuterWall);

                m_customMesh.vertices[vertexIndex] = center + rot * right * offset;

                m_uvs.x = anglePercent;
                m_uvs.y = 1f - uvPercent;
                m_customMesh.uv[vertexIndex] = m_uvs;

                Vector3 vn = m_hasThickness ? center - m_customMesh.vertices[vertexIndex] : m_customMesh.vertices[vertexIndex] - center;
                m_customMesh.normals[vertexIndex] = Vector3.Normalize(vn);

                vertexIndex++;
            }
        }
    }

    private float GetVertexThicknessOffset(bool outerRadius)
    {
        float offset = m_radius;
        if (m_hasThickness)
        {
            if (m_shellType == TubeGenerator.eRadius.INWARD)
            {
                offset = outerRadius ? m_radius : m_radius - m_thickness;
            }
            else if (m_shellType == TubeGenerator.eRadius.OUTWARD)
            {
                offset = outerRadius ? m_radius + m_thickness : m_radius;
            }
        }

        return offset;
    }

    private float GetCapsVertexOffset()
    {
        float offset = 1f;

        if (m_shellType == TubeGenerator.eRadius.INWARD)
        {
            offset = (m_radius - m_thickness) / m_radius;
        }
        else if (m_shellType == TubeGenerator.eRadius.OUTWARD)
        {
            offset = m_radius / (m_radius + m_thickness);
        }

        return offset;
    }

    void MakeInnerSide(CustomMesh input)
    {
        int vertexHalf = input.vertices.Length / 2;
        int trisHalf = input.triangles.Length / 2;

        for (int i = 0; i < trisHalf; i += 3)
        {
            input.triangles[i + trisHalf + 2] = input.triangles[i] + vertexHalf;
            input.triangles[i + trisHalf + 1] = input.triangles[i + 1] + vertexHalf;
            input.triangles[i + trisHalf] = input.triangles[i + 2] + vertexHalf;
        }
    }

    void GenerateCaps()
    {
        AllocateMeshForCaps();

        //Start Cap
        int index = 0;

        //outer ring
        for (int i = 0; i < m_sides + 1; i++)
        {
            index = m_bodyVertexCount + i;
            m_customMesh.vertices[index] = m_customMesh.vertices[i];

            Vector3 dir = -m_samples[0].direction;
            m_customMesh.normals[index] = dir;

            float anglePercent = (float)(i) / m_sides;
            m_customMesh.uv[index] = Quaternion.AngleAxis(anglePercent * 360f, -dir) * Vector2.right * 0.5f + (Vector3.right + Vector3.up) * 0.5f;
        }

        //inner ring
        for (int i = 0; i < m_sides + 1; i++)
        {
            index++;
            m_customMesh.vertices[index] = m_customMesh.vertices[m_bodyVertexCount / 2 + i];

            Vector3 dir = -m_samples[0].direction;
            m_customMesh.normals[index] = dir;

            float anglePercent = (float)(i) / m_sides;
            float offset = GetCapsVertexOffset();
            m_customMesh.uv[index] = Quaternion.AngleAxis(anglePercent * 360f, -dir) * Vector2.right * 0.5f * offset + (Vector3.right + Vector3.up) * 0.5f;
        }

        //end cap
        //outer ring
        for (int i = 0; i < m_sides + 1; i++)
        {
            index++;
            m_customMesh.vertices[index] = m_customMesh.vertices[m_bodyVertexCount / 2 - (m_sides + 1) + i];

            Vector3 dir = m_samples[m_samples.Count - 1].direction;
            m_customMesh.normals[index] = dir;

            float anglePercent = (float)(i) / m_sides;
            m_customMesh.uv[index] = Quaternion.AngleAxis(anglePercent * 360f + 180f, -dir) * Vector2.right * 0.5f + (Vector3.right + Vector3.up) * 0.5f;
        }

        //inner ring
        for (int i = 0; i < m_sides + 1; i++)
        {
            index++;
            m_customMesh.vertices[index] = m_customMesh.vertices[m_bodyVertexCount - (m_sides + 1) + i];

            Vector3 dir = m_samples[m_samples.Count - 1].direction;
            m_customMesh.normals[index] = dir;

            float anglePercent = (float)(i) / m_sides;
            float offset = GetCapsVertexOffset();

            m_customMesh.uv[index] = Quaternion.AngleAxis(anglePercent * 360f + 180f, -dir) * Vector2.right * 0.5f * offset + (Vector3.right + Vector3.up) * 0.5f;
        }

        GenerateCapsPlaneTriangles();
    }

    void AllocateMeshForCaps()
    {
        //outer radius, inner, start & and caps
        m_capVertexCount = (m_sides + 1) * 2 * 2;
        int vertexCount = m_bodyVertexCount + m_capVertexCount;
        if (m_customMesh.vertexCount != vertexCount)
        {
            Vector3[] vs = new Vector3[vertexCount];
            for (int i = 0; i < m_customMesh.vertices.Length; i++)
            {
                vs[i] = m_customMesh.vertices[i];
            }

            m_customMesh.vertices = vs;

            Vector3[] ns = new Vector3[vertexCount];
            for (int i = 0; i < m_customMesh.normals.Length; i++)
            {
                ns[i] = m_customMesh.normals[i];
            }

            m_customMesh.normals = ns;

            Vector2[] uvs = new Vector2[vertexCount];
            for (int i = 0; i < m_customMesh.uv.Length; i++)
            {
                uvs[i] = m_customMesh.uv[i];
            }

            m_customMesh.uv = uvs;
        }

        //sides, tris in side, vertex/tris, two caps
        m_capTrisCount = m_sides * 2 * 3 * 2;
        int trisCount = m_bodyTrisCount + m_capTrisCount;
        if (m_customMesh.triangles.Length != trisCount)
        {
            int[] tris = new int[trisCount];
            for (int i = 0; i < m_customMesh.triangles.Length; i++)
            {
                tris[i] = m_customMesh.triangles[i];
            }

            m_customMesh.triangles = tris;

        }
    }

    int[] GenerateCapsPlaneTriangles()
    {
        int nbFaces = m_sides;
        int t = m_bodyTrisCount;
        for (int face = 0; face < nbFaces; face++)
        {
            m_customMesh.triangles[t++] = m_bodyVertexCount + face + m_sides + 1;
            m_customMesh.triangles[t++] = m_bodyVertexCount + face + 1;
            m_customMesh.triangles[t++] = m_bodyVertexCount + face;

            m_customMesh.triangles[t++] = m_bodyVertexCount + face + 1;
            m_customMesh.triangles[t++] = m_bodyVertexCount + face + m_sides + 1;
            m_customMesh.triangles[t++] = m_bodyVertexCount + face + m_sides + 2;
        }

        int halfCapVert = m_capVertexCount / 2;
        for (int face = 0; face < nbFaces; face++)
        {
            m_customMesh.triangles[t++] = m_bodyVertexCount + halfCapVert + face;
            m_customMesh.triangles[t++] = m_bodyVertexCount + halfCapVert + face + 1;
            m_customMesh.triangles[t++] = m_bodyVertexCount + halfCapVert + face + m_sides + 1;

            m_customMesh.triangles[t++] = m_bodyVertexCount + halfCapVert + face + m_sides + 2;
            m_customMesh.triangles[t++] = m_bodyVertexCount + halfCapVert + face + m_sides + 1;
            m_customMesh.triangles[t++] = m_bodyVertexCount + halfCapVert + face + 1;
        }

        return m_customMesh.triangles;
    }

    int[] GenerateBodyPlaneTriangles()
    {
        int nbFaces = m_sides * (m_samples.Count - 1);
        int t = 0;
        int g = m_sides + 1;
        for (int face = 0; face < nbFaces + m_samples.Count - 2; face++)
        {
            if ((float)(face + 1) % (float)g == 0f && face != 0) face++;

            m_customMesh.triangles[t++] = face;
            m_customMesh.triangles[t++] = face + 1;
            m_customMesh.triangles[t++] = face + m_sides + 1;

            m_customMesh.triangles[t++] = face + 1;
            m_customMesh.triangles[t++] = face + m_sides + 2;
            m_customMesh.triangles[t++] = face + m_sides + 1;
        }

        return m_customMesh.triangles;
    }
}
