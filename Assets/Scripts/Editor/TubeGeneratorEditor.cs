using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(TubeGenerator))]
public class TubeGeneratorEditor : Editor
{
    private bool m_toggleAddPoints;

    private bool m_isParameterMode;

    private TubeGenerator m_generator;

    SerializedProperty m_useThickness;

    SerializedProperty m_parameterMode;

    SerializedProperty m_points;


    private void OnEnable()
    {
        m_generator = (TubeGenerator)target;

        m_useThickness = serializedObject.FindProperty("hasThickness");
        m_points = serializedObject.FindProperty("points");
        m_parameterMode = serializedObject.FindProperty("ParameterMode");
    }

    private void OnDisable()
    {
        m_toggleAddPoints = false;
    }

    public override void OnInspectorGUI()
    {
        //DrawDefaultInspector();

        serializedObject.Update();

        EditorGUILayout.BeginHorizontal();
        m_toggleAddPoints = GUILayout.Toggle(m_toggleAddPoints, "Add Point", "button");

        if (GUILayout.Button("Remove Last Point"))
        {
            m_generator.RemoveLastPoint();
        }

        EditorGUILayout.EndHorizontal();

        if(m_generator.GetSamplesCount() == 0)
        {
            EditorGUILayout.LabelField("NEED REBUILD! SAMPLES COUNT IS 0!", EditorStyles.boldLabel);
            if (GUILayout.Button("Rebuild Mesh"))
            {
                m_generator.RebuildMesh();
            }
        }

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("points"), true);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("lods"), true);

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("radius"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(m_parameterMode);

        m_isParameterMode = m_parameterMode.enumValueIndex == 1;

        EditorGUI.BeginDisabledGroup(m_isParameterMode);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("By Parameters", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("sides"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("subdivision"));
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(!m_isParameterMode);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("By Polycount", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("triangles"));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spline Mode", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("SplineMode"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Thickness", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(m_useThickness);

        EditorGUI.BeginDisabledGroup(!m_useThickness.boolValue);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("thickness"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("shellType"));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("LOD's", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("numberLODs"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("koeffLOD"));
        var list = m_generator.GetLODInfo();
        if (list.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("LOD's Info", EditorStyles.boldLabel);

            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(list[i].name);
                EditorGUILayout.LabelField(list[i].trisCount.ToString());
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Build LOD's"))
        {
            m_generator.BuildLODs();
        }

        if (GUILayout.Button("Clear LOD's"))
        {
            m_generator.ClearLODs();
        }


        if (serializedObject.hasModifiedProperties)
        {
            serializedObject.ApplyModifiedProperties();
            m_generator.RebuildMesh();
        }
    }

    Plane plane = new Plane(Vector3.up, Vector3.zero);
    private void OnSceneGUI()
    {
        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;


        float hit = 100f;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (plane.Raycast(ray, out hit))
        {
            Vector3 relativePoint = ray.GetPoint(hit);
            Handles.color = Color.green;
            if (m_toggleAddPoints)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));


                SerializedProperty sp = m_points.GetArrayElementAtIndex(m_points.arraySize - 1);
                Transform tp = (Transform)sp.objectReferenceValue;
                Vector3 lastPoint = tp.position;
                Handles.DrawLine(lastPoint, relativePoint);
                switch (e.GetTypeForControl(controlID))
                {
                    case EventType.MouseDown:
                        if (e.button == 0 && !e.alt && !e.control && !e.shift)
                        {
                            m_generator.AddTubePoint(relativePoint);
                            m_generator.RebuildMesh();
                        }
                        break;
                    case EventType.Layout:
                        HandleUtility.AddDefaultControl(controlID);
                        break;
                }
            }
        }
    }

}
