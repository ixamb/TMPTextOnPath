using TMPro;
using UnityEditor;
using UnityEngine;
#if TEXTONPATH_SPLINES
using UnityEngine.Splines;
#endif

namespace TMPTextOnPath.Editor
{
    [CustomEditor(typeof(TextOnPath)), CanEditMultipleObjects]
    public class TextOnPathEditor : UnityEditor.Editor
    {
        static readonly Color k_PathColor = new Color(0.3f, 0.8f, 1f, 0.9f);
        static readonly Color k_AnchorColor = new Color(1f, 0.85f, 0.2f, 1f);
        static readonly Color k_TangentColor = new Color(1f, 0.45f, 0.45f, 1f);

        SerializedProperty m_Shape, m_Radius, m_EllipseRadii, m_StartAngle, m_SweepAngle, m_Clockwise;
        SerializedProperty m_Length, m_Amplitude, m_Wavelength, m_Phase, m_CurveHeight, m_Curve;
        SerializedProperty m_BezierPoints, m_ClosedBezier, m_Reference, m_SplineContainer, m_SplineIndex;
        SerializedProperty m_PathOffset, m_PathRotation, m_Resolution;
        SerializedProperty m_OffsetMode, m_StartOffset, m_Alignment, m_VerticalAnchor, m_VerticalOffset, m_Reverse;
        SerializedProperty m_Orientation, m_GlyphRotation, m_FlipGlyphsX, m_FlipGlyphsY, m_StretchToFit, m_FitRange;
        SerializedProperty m_Overflow, m_ShowPathGizmo;

        Vector3[] m_Polyline = new Vector3[0];

        void OnEnable()
        {
            m_Shape = serializedObject.FindProperty("m_Shape");
            m_Radius = serializedObject.FindProperty("m_Radius");
            m_EllipseRadii = serializedObject.FindProperty("m_EllipseRadii");
            m_StartAngle = serializedObject.FindProperty("m_StartAngle");
            m_SweepAngle = serializedObject.FindProperty("m_SweepAngle");
            m_Clockwise = serializedObject.FindProperty("m_Clockwise");
            m_Length = serializedObject.FindProperty("m_Length");
            m_Amplitude = serializedObject.FindProperty("m_Amplitude");
            m_Wavelength = serializedObject.FindProperty("m_Wavelength");
            m_Phase = serializedObject.FindProperty("m_Phase");
            m_CurveHeight = serializedObject.FindProperty("m_CurveHeight");
            m_Curve = serializedObject.FindProperty("m_Curve");
            m_BezierPoints = serializedObject.FindProperty("m_BezierPoints");
            m_ClosedBezier = serializedObject.FindProperty("m_ClosedBezier");
            m_Reference = serializedObject.FindProperty("m_Reference");
            m_SplineContainer = serializedObject.FindProperty("m_SplineContainer");
            m_SplineIndex = serializedObject.FindProperty("m_SplineIndex");
            m_PathOffset = serializedObject.FindProperty("m_PathOffset");
            m_PathRotation = serializedObject.FindProperty("m_PathRotation");
            m_Resolution = serializedObject.FindProperty("m_Resolution");
            m_OffsetMode = serializedObject.FindProperty("m_OffsetMode");
            m_StartOffset = serializedObject.FindProperty("m_StartOffset");
            m_Alignment = serializedObject.FindProperty("m_Alignment");
            m_VerticalAnchor = serializedObject.FindProperty("m_VerticalAnchor");
            m_VerticalOffset = serializedObject.FindProperty("m_VerticalOffset");
            m_Reverse = serializedObject.FindProperty("m_Reverse");
            m_Orientation = serializedObject.FindProperty("m_Orientation");
            m_GlyphRotation = serializedObject.FindProperty("m_GlyphRotation");
            m_FlipGlyphsX = serializedObject.FindProperty("m_FlipGlyphsX");
            m_FlipGlyphsY = serializedObject.FindProperty("m_FlipGlyphsY");
            m_StretchToFit = serializedObject.FindProperty("m_StretchToFit");
            m_FitRange = serializedObject.FindProperty("m_FitRange");
            m_Overflow = serializedObject.FindProperty("m_Overflow");
            m_ShowPathGizmo = serializedObject.FindProperty("m_ShowPathGizmo");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (((TextOnPath)target).GetComponent<TMP_Text>() == null)
                EditorGUILayout.HelpBox("This component requires a TextMeshPro or TextMeshPro - Text (UI) component on the same GameObject.", MessageType.Error);

            EditorGUILayout.LabelField("Path", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_Shape);

            if (!m_Shape.hasMultipleDifferentValues)
            {
                EditorGUI.indentLevel++;
                DrawShapeSettings((PathShape)m_Shape.enumValueIndex);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(m_PathOffset);
            EditorGUILayout.PropertyField(m_PathRotation);
            EditorGUILayout.PropertyField(m_Resolution);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Text", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_OffsetMode);
            if (!m_OffsetMode.hasMultipleDifferentValues && (PathOffsetMode)m_OffsetMode.enumValueIndex == PathOffsetMode.Normalized)
                EditorGUILayout.Slider(m_StartOffset, 0f, 1f);
            else
                EditorGUILayout.PropertyField(m_StartOffset);
            EditorGUILayout.PropertyField(m_Alignment);
            EditorGUILayout.PropertyField(m_VerticalAnchor);
            EditorGUILayout.PropertyField(m_VerticalOffset);
            EditorGUILayout.PropertyField(m_Reverse);
            EditorGUILayout.PropertyField(m_Orientation);
            EditorGUILayout.PropertyField(m_GlyphRotation);
            EditorGUILayout.PropertyField(m_FlipGlyphsX, new GUIContent("Mirror Glyphs X"));
            EditorGUILayout.PropertyField(m_FlipGlyphsY, new GUIContent("Mirror Glyphs Y"));
            EditorGUILayout.PropertyField(m_StretchToFit);
            if (m_StretchToFit.boolValue || m_StretchToFit.hasMultipleDifferentValues)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_FitRange);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(m_Overflow);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_ShowPathGizmo);

            serializedObject.ApplyModifiedProperties();

            if (!m_Shape.hasMultipleDifferentValues && (PathShape)m_Shape.enumValueIndex == PathShape.Bezier)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Point"))
                        ForEachTarget("Add Bezier Point", AppendBezierPoint);
                    if (GUILayout.Button("Remove Last Point"))
                        ForEachTarget("Remove Bezier Point", t =>
                        {
                            if (t.BezierPointCount > 2)
                                t.RemoveBezierPoint(t.BezierPointCount - 1);
                        });
                }
            }
        }

        void DrawShapeSettings(PathShape shape)
        {
            switch (shape)
            {
                case PathShape.Circle:
                    EditorGUILayout.PropertyField(m_Radius);
                    EditorGUILayout.PropertyField(m_StartAngle);
                    EditorGUILayout.PropertyField(m_Clockwise);
                    break;

                case PathShape.Ellipse:
                    EditorGUILayout.PropertyField(m_EllipseRadii, new GUIContent("Radii"));
                    EditorGUILayout.PropertyField(m_StartAngle);
                    EditorGUILayout.PropertyField(m_Clockwise);
                    break;

                case PathShape.Arc:
                    EditorGUILayout.PropertyField(m_Radius);
                    EditorGUILayout.PropertyField(m_StartAngle);
                    EditorGUILayout.PropertyField(m_SweepAngle);
                    EditorGUILayout.PropertyField(m_Clockwise);
                    break;

                case PathShape.Line:
                    EditorGUILayout.PropertyField(m_Length);
                    break;

                case PathShape.Wave:
                    EditorGUILayout.PropertyField(m_Length);
                    EditorGUILayout.PropertyField(m_Amplitude);
                    EditorGUILayout.PropertyField(m_Wavelength);
                    EditorGUILayout.PropertyField(m_Phase);
                    break;

                case PathShape.Curve:
                    EditorGUILayout.PropertyField(m_Length);
                    EditorGUILayout.PropertyField(m_CurveHeight, new GUIContent("Height"));
                    EditorGUILayout.PropertyField(m_Curve);
                    break;

                case PathShape.Bezier:
                    EditorGUILayout.PropertyField(m_ClosedBezier, new GUIContent("Closed"));
                    EditorGUILayout.PropertyField(m_BezierPoints, new GUIContent("Points"), true);
                    EditorGUILayout.HelpBox("Move the points and their tangents directly in the Scene view.", MessageType.None);
                    break;

                case PathShape.Reference:
                    EditorGUILayout.PropertyField(m_Reference, new GUIContent("Reference Text"));
                    if (m_Reference.objectReferenceValue == null && !m_Reference.hasMultipleDifferentValues)
                        EditorGUILayout.HelpBox("Assign another Text On Path: this text will follow its path, with its own offset, alignment and style.", MessageType.Info);
                    break;

                case PathShape.Spline:
#if TEXTONPATH_SPLINES
                    EditorGUILayout.ObjectField(m_SplineContainer, typeof(SplineContainer), new GUIContent("Spline Container"));
                    EditorGUILayout.PropertyField(m_SplineIndex);
                    EditorGUILayout.HelpBox("The spline is projected onto the text's local XY plane.", MessageType.None);
#else
                    EditorGUILayout.HelpBox("Install the Splines package (com.unity.splines 2.0+) to use this mode.", MessageType.Warning);
#endif
                    break;
            }
        }

        static void AppendBezierPoint(TextOnPath t)
        {
            int n = t.BezierPointCount;
            if (n == 0)
            {
                t.AddBezierPoint(new BezierPoint(Vector2.zero, Vector2.right));
                return;
            }
            BezierPoint last = t.GetBezierPoint(n - 1);
            Vector2 dir = last.outTangent.sqrMagnitude > 1e-6f ? last.outTangent : Vector2.right;
            t.AddBezierPoint(new BezierPoint(last.position + dir * 3f, dir));
        }

        void ForEachTarget(string undoName, System.Action<TextOnPath> action)
        {
            foreach (Object o in targets)
            {
                var t = (TextOnPath)o;
                Undo.RecordObject(t, undoName);
                action(t);
                EditorUtility.SetDirty(t);
                t.Refresh();
            }
            serializedObject.Update();
        }

        // ---------------------------------------------------------------- Scene view

        void OnSceneGUI()
        {
            var t = (TextOnPath)target;
            if (!t.isActiveAndEnabled)
                return;

            PathSampler sampler = t.Sampler;
            if (sampler.Count == 0)
                return;

            using (new Handles.DrawingScope(t.transform.localToWorldMatrix))
            {
                DrawPath(sampler);
                DrawCenterHandle(t);

                switch (t.Shape)
                {
                    case PathShape.Circle:
                    case PathShape.Arc:
                        DrawRadiusHandle(t);
                        break;
                    case PathShape.Ellipse:
                        DrawEllipseHandles(t);
                        break;
                    case PathShape.Bezier:
                        DrawBezierHandles(t);
                        break;
                }

                DrawAnchorHandle(t);
            }
        }

        void DrawPath(PathSampler sampler)
        {
            if (m_Polyline.Length != sampler.Count)
                m_Polyline = new Vector3[sampler.Count];
            for (int i = 0; i < sampler.Count; i++)
                m_Polyline[i] = sampler.GetPoint(i);

            Handles.color = k_PathColor;
            Handles.DrawAAPolyLine(3f, m_Polyline);

            // Small arrow showing the direction of the path.
            if (sampler.Length > 0f && sampler.Evaluate(sampler.Length * 0.02f, PathOverflow.Extrapolate, out Vector2 p, out Vector2 tan))
            {
                float s = HandleUtility.GetHandleSize(p) * 0.15f;
                Vector2 n = new Vector2(-tan.y, tan.x);
                Handles.DrawAAPolyLine(3f, p - tan * s + n * s * 0.6f, p, p - tan * s - n * s * 0.6f);
            }
        }

        static void DrawCenterHandle(TextOnPath t)
        {
            Vector3 center = t.PathOffset;
            Handles.color = k_PathColor;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = FreeMove(center, HandleUtility.GetHandleSize(center) * 0.05f, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Move Path");
                t.PathOffset = moved;
                Commit(t);
            }
        }

        static void DrawAnchorHandle(TextOnPath t)
        {
            Vector3 anchor = t.GetAnchorLocalPosition();
            Handles.color = k_AnchorColor;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = FreeMove(anchor, HandleUtility.GetHandleSize(anchor) * 0.07f, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Move Text Offset");
                t.SetStartOffsetFromLocalPoint(moved);
                Commit(t);
            }
        }

        static void DrawRadiusHandle(TextOnPath t)
        {
            float a = t.StartAngle * Mathf.Deg2Rad;
            Vector3 handle = t.PathToLocal(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * t.Radius);
            Handles.color = k_PathColor;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = FreeMove(handle, HandleUtility.GetHandleSize(handle) * 0.05f, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Change Radius");
                t.Radius = t.LocalToPath(moved).magnitude;
                Commit(t);
            }
        }

        static void DrawEllipseHandles(TextOnPath t)
        {
            Vector2 radii = t.EllipseRadii;
            Handles.color = k_PathColor;

            Vector3 hx = t.PathToLocal(new Vector2(radii.x, 0f));
            EditorGUI.BeginChangeCheck();
            Vector3 movedX = FreeMove(hx, HandleUtility.GetHandleSize(hx) * 0.05f, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Change Ellipse");
                t.EllipseRadii = new Vector2(Mathf.Abs(t.LocalToPath(movedX).x), radii.y);
                Commit(t);
            }

            Vector3 hy = t.PathToLocal(new Vector2(0f, radii.y));
            EditorGUI.BeginChangeCheck();
            Vector3 movedY = FreeMove(hy, HandleUtility.GetHandleSize(hy) * 0.05f, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t, "Change Ellipse");
                t.EllipseRadii = new Vector2(t.EllipseRadii.x, Mathf.Abs(t.LocalToPath(movedY).y));
                Commit(t);
            }
        }

        static void DrawBezierHandles(TextOnPath t)
        {
            for (int i = 0; i < t.BezierPointCount; i++)
            {
                BezierPoint bp = t.GetBezierPoint(i);
                Vector3 pos = t.PathToLocal(bp.position);
                Vector3 inPos = t.PathToLocal(bp.position + bp.inTangent);
                Vector3 outPos = t.PathToLocal(bp.position + bp.outTangent);

                Handles.color = k_TangentColor;
                Handles.DrawLine(pos, inPos);
                Handles.DrawLine(pos, outPos);

                EditorGUI.BeginChangeCheck();
                Vector3 newIn = FreeMove(inPos, HandleUtility.GetHandleSize(inPos) * 0.04f, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(t, "Move Bezier Tangent");
                    bp.inTangent = t.LocalToPath(newIn) - bp.position;
                    if (!bp.broken)
                        bp.outTangent = -bp.inTangent;
                    t.SetBezierPoint(i, bp);
                    Commit(t);
                }

                EditorGUI.BeginChangeCheck();
                Vector3 newOut = FreeMove(outPos, HandleUtility.GetHandleSize(outPos) * 0.04f, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(t, "Move Bezier Tangent");
                    bp.outTangent = t.LocalToPath(newOut) - bp.position;
                    if (!bp.broken)
                        bp.inTangent = -bp.outTangent;
                    t.SetBezierPoint(i, bp);
                    Commit(t);
                }

                Handles.color = Color.white;
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = FreeMove(pos, HandleUtility.GetHandleSize(pos) * 0.06f, Handles.RectangleHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(t, "Move Bezier Point");
                    bp.position = t.LocalToPath(newPos);
                    t.SetBezierPoint(i, bp);
                    Commit(t);
                }
            }
        }

        static void Commit(TextOnPath t)
        {
            EditorUtility.SetDirty(t);
            t.Refresh();
        }

        static Vector3 FreeMove(Vector3 position, float size, Handles.CapFunction cap)
        {
#if UNITY_2022_1_OR_NEWER
            Vector3 result = Handles.FreeMoveHandle(position, size, Vector3.zero, cap);
#else
            Vector3 result = Handles.FreeMoveHandle(position, Quaternion.identity, size, Vector3.zero, cap);
#endif
            result.z = 0f;
            return result;
        }
    }
}
