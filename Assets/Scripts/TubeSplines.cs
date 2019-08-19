using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TubeSplines
{
    public List<TubePoint> Samples { get; private set; } = new List<TubePoint>();

    public void CalculateSamples(List<Transform> points, TubeGenerator.eSplineMode mode, int subdiv)
    {
        Samples.Clear();

        if (mode == TubeGenerator.eSplineMode.Linear)
        {
            CalculateLine(points, subdiv);
        }
        else if (mode == TubeGenerator.eSplineMode.Spline)
        {
            CalculateSpline(points, subdiv);
        }
    }

    private void CalculateSpline(List<Transform> points, int subdiv)
    {
        Vector3 p0;
        Vector3 p1;
        Vector3 m0;
        Vector3 m1;

        subdiv++;
        float pointStep = 1f / subdiv;
        for (int i = 0; i < points.Count - 1; i++)
        {
            p0 = points[i].transform.position;
            p1 = points[i + 1].transform.position;

            // m0
            if (i == 0)
            {
                m0 = p1 - p0;
            }
            else
            {
                m0 = 0.5f * (p1 - points[i - 1].transform.position);
            }

            // m1
            if (i < points.Count - 2)
            {
                m1 = 0.5f * (points[(i + 2) % points.Count].transform.position - p0);
            }
            else
            {
                m1 = p1 - p0;
            }

            Vector3 position;
            float t;

            if (i == points.Count - 2)
            {
                pointStep = 1f / (subdiv - 1);
            }

            for (int j = 0; j < subdiv; j++)
            {
                t = j * pointStep;
                Vector3 tangent;
                position = Interpolate(p0, p1, m0, m1, t, out tangent);

                TubePoint tpmain = new TubePoint()
                {
                    direction = tangent,
                    position = position,
                    right = Vector3.Cross(Vector3.up, tangent).normalized
                };

                Samples.Add(tpmain);
            }
        }
    }

    public Vector3 Interpolate(Vector3 start, Vector3 end, Vector3 tanPoint1, Vector3 tanPoint2, float t, out Vector3 tangent)
    {
        tangent = (6 * t * t - 6 * t) * start
            + (3 * t * t - 4 * t + 1) * tanPoint1
            + (-6 * t * t + 6 * t) * end
            + (3 * t * t - 2 * t) * tanPoint2;
        return Interpolate(start, end, tanPoint1, tanPoint2, t);
    }

    public Vector3 Interpolate(Vector3 start, Vector3 end, Vector3 tanPoint1, Vector3 tanPoint2, float t)
    {
        Vector3 position = (2.0f * t * t * t - 3.0f * t * t + 1.0f) * start
            + (t * t * t - 2.0f * t * t + t) * tanPoint1
            + (-2.0f * t * t * t + 3.0f * t * t) * end
            + (t * t * t - t * t) * tanPoint2;

        return position;
    }

    private void CalculateLine(List<Transform> points, int subdiv)
    {
        List<TubePoint> mainSamples = new List<TubePoint>();
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var pf = p.forward;

            if (i == 0)
            {
                pf = (points[i + 1].position - p.position).normalized;
            }
            else if (i == points.Count - 1)
            {
                pf = (p.position - points[i - 1].position).normalized;
            }
            else
            {
                var dir0 = (p.position - points[i - 1].position).normalized;
                var dir1 = (points[i + 1].position - p.position).normalized;
                var res = dir0 + dir1;

                pf = res.normalized;
            }

            TubePoint tpmain = new TubePoint()
            {
                IsPoint = true,
                direction = pf,
                position = p.position,
                right = Vector3.Cross(p.up, pf).normalized,
            };

            mainSamples.Add(tpmain);
        }

        for (int i = 0; i < mainSamples.Count; i++)
        {
            if (i == mainSamples.Count - 1)
            {
                TubePoint tpmain = mainSamples[i];
                Samples.Add(tpmain);
            }
            else
            {
                Samples.Add(mainSamples[i]);

                Vector3 dir = mainSamples[i + 1].position - mainSamples[i].position;

                dir /= subdiv;

                for (int j = 0; j < subdiv - 1; j++)
                {
                    TubePoint tp = new TubePoint()
                    {
                        IsPoint = false,
                        direction = dir.normalized,
                        position = mainSamples[i].position + dir * (j + 1),
                        right = Vector3.Cross(Vector3.up, dir.normalized).normalized
                    };

                    Samples.Add(tp);
                }
            }
        }
    }
}
