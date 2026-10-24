using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if TEXTONPATH_SPLINES
using UnityEngine.Splines;
#endif

namespace TMPTextOnPath
{
    /// <summary>
    /// Makes a TextMesh Pro text (TextMeshPro 3D or TextMeshProUGUI) follow a configurable path.
    /// Each character is placed rigidly on the path, so TMP material effects (outline, glow, underlay…)
    /// and rich text are preserved. The path lives in the text's local space (the same space as its mesh).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Text On Path/Text On Path (TMP)")]
    public class TextOnPath : MonoBehaviour
    {
        const int k_MaxReferenceDepth = 8;

        // ---- Path ----
        [SerializeField] PathShape m_Shape = PathShape.Circle;

        [Tooltip("Radius of the circle or arc.")]
        [SerializeField, Min(0f)] float m_Radius = 150f;
        [Tooltip("Half-axes of the ellipse.")]
        [SerializeField] Vector2 m_EllipseRadii = new Vector2(220f, 130f);
        [Tooltip("Angle in degrees (0 = right, 90 = top) where the circle, ellipse or arc starts.")]
        [SerializeField] float m_StartAngle = 90f;
        [Tooltip("Angular extent of the arc, in degrees.")]
        [SerializeField, Range(0f, 360f)] float m_SweepAngle = 180f;
        [Tooltip("Direction in which the circle, ellipse or arc is traversed.")]
        [SerializeField] bool m_Clockwise = true;

        [Tooltip("Length of the line, wave or curve.")]
        [SerializeField, Min(0f)] float m_Length = 430f;
        [SerializeField] float m_Amplitude = 36f;
        [SerializeField, Min(0.001f)] float m_Wavelength = 215f;
        [Tooltip("Phase of the wave, in degrees.")]
        [SerializeField] float m_Phase;
        [Tooltip("Height applied to the AnimationCurve (a curve value of 1 equals this height).")]
        [SerializeField] float m_CurveHeight = 110f;
        [Tooltip("Curve profile. X goes from 0 to 1 along the length, Y is multiplied by the height.")]
        [SerializeField] AnimationCurve m_Curve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.5f, 1f, 0f, 0f), new Keyframe(1f, 0f, 0f, 0f));

        [SerializeField] List<BezierPoint> m_BezierPoints = new List<BezierPoint>();
        [Tooltip("Connects the last Bezier point back to the first one.")]
        [SerializeField] bool m_ClosedBezier;

        [Tooltip("Another TextOnPath whose path is reused (follows it live, whatever the relative transforms).")]
        [SerializeField] TextOnPath m_Reference;
        [Tooltip("A SplineContainer from the Unity Splines package (com.unity.splines).")]
        [SerializeField] Component m_SplineContainer;
        [Tooltip("Index of the spline inside the container.")]
        [SerializeField, Min(0)] int m_SplineIndex;

        [Tooltip("Offset of the path in the text's local space.")]
        [SerializeField] Vector2 m_PathOffset;
        [Tooltip("Rotation of the path, in degrees.")]
        [SerializeField] float m_PathRotation;
        [Tooltip("Number of samples used to approximate the path.")]
        [SerializeField, Range(8, 1024)] int m_Resolution = 128;

        // ---- Text ----
        [Tooltip("Unit of Start Offset: normalized (0..1) or a distance along the path.")]
        [SerializeField] PathOffsetMode m_OffsetMode = PathOffsetMode.Normalized;
        [Tooltip("Position of the anchor point along the path. On a closed path the value wraps around: animate it to scroll or rotate the text.")]
        [SerializeField] float m_StartOffset;
        [Tooltip("Position of the text relative to the anchor point.")]
        [SerializeField] PathTextAlignment m_Alignment = PathTextAlignment.Center;
        [Tooltip("Which part of the text lies on the path.")]
        [SerializeField] PathVerticalAnchor m_VerticalAnchor = PathVerticalAnchor.Baseline;
        [Tooltip("Distance between the path and the text along the normal (positive = towards the top of the glyphs).")]
        [SerializeField] float m_VerticalOffset;
        [Tooltip("Traverses the path backwards: the text moves to the other side of the path and stays readable (e.g. the bottom text of a badge).")]
        [SerializeField] bool m_Reverse;
        [SerializeField] PathGlyphOrientation m_Orientation = PathGlyphOrientation.FollowPath;
        [Tooltip("Extra rotation applied to every glyph, in degrees.")]
        [SerializeField] float m_GlyphRotation;
        [Tooltip("Mirrors each glyph horizontally.")]
        [SerializeField] bool m_FlipGlyphsX;
        [Tooltip("Mirrors each glyph vertically.")]
        [SerializeField] bool m_FlipGlyphsY;
        [Tooltip("Spreads the characters so the text covers a fraction of the path (glyphs are not scaled).")]
        [SerializeField] bool m_StretchToFit;
        [Tooltip("Fraction of the path covered when Stretch To Fit is enabled.")]
        [SerializeField, Range(0f, 1f)] float m_FitRange = 1f;
        [Tooltip("What to do with characters running past the ends of an open path.")]
        [SerializeField] PathOverflow m_Overflow = PathOverflow.Extrapolate;

        [SerializeField] bool m_ShowPathGizmo = true;

        TMP_Text m_Text;
        readonly PathSampler m_Sampler = new PathSampler();
        readonly List<Vector2> m_Buffer = new List<Vector2>(256);
        int m_BuiltHash;
        bool m_HasBuilt;
        int m_AppliedHash;
        bool m_IsApplying;
        static int s_ReferenceDepth;

        // ---------------------------------------------------------------- Public API

        public TMP_Text Text => m_Text != null ? m_Text : (m_Text = GetComponent<TMP_Text>());

        public PathShape Shape { get => m_Shape; set => m_Shape = value; }
        public float Radius { get => m_Radius; set => m_Radius = Mathf.Max(0f, value); }
        public Vector2 EllipseRadii { get => m_EllipseRadii; set => m_EllipseRadii = value; }
        public float StartAngle { get => m_StartAngle; set => m_StartAngle = value; }
        public float SweepAngle { get => m_SweepAngle; set => m_SweepAngle = Mathf.Clamp(value, 0f, 360f); }
        public bool Clockwise { get => m_Clockwise; set => m_Clockwise = value; }
        public float Length { get => m_Length; set => m_Length = Mathf.Max(0f, value); }
        public float Amplitude { get => m_Amplitude; set => m_Amplitude = value; }
        public float Wavelength { get => m_Wavelength; set => m_Wavelength = Mathf.Max(0.001f, value); }
        public float Phase { get => m_Phase; set => m_Phase = value; }
        public float CurveHeight { get => m_CurveHeight; set => m_CurveHeight = value; }
        public AnimationCurve Curve { get => m_Curve; set => m_Curve = value; }
        public bool ClosedBezier { get => m_ClosedBezier; set => m_ClosedBezier = value; }
        public TextOnPath Reference { get => m_Reference; set => m_Reference = value; }
        /// <summary>Must be a UnityEngine.Splines.SplineContainer (requires com.unity.splines).</summary>
        public Component SplineContainer { get => m_SplineContainer; set => m_SplineContainer = value; }
        public int SplineIndex { get => m_SplineIndex; set => m_SplineIndex = Mathf.Max(0, value); }
        public Vector2 PathOffset { get => m_PathOffset; set => m_PathOffset = value; }
        public float PathRotation { get => m_PathRotation; set => m_PathRotation = value; }
        public int Resolution { get => m_Resolution; set => m_Resolution = Mathf.Clamp(value, 8, 1024); }

        public PathOffsetMode OffsetMode { get => m_OffsetMode; set => m_OffsetMode = value; }
        public float StartOffset { get => m_StartOffset; set => m_StartOffset = value; }
        public PathTextAlignment Alignment { get => m_Alignment; set => m_Alignment = value; }
        public PathVerticalAnchor VerticalAnchor { get => m_VerticalAnchor; set => m_VerticalAnchor = value; }
        public float VerticalOffset { get => m_VerticalOffset; set => m_VerticalOffset = value; }
        public bool Reverse { get => m_Reverse; set => m_Reverse = value; }
        public PathGlyphOrientation Orientation { get => m_Orientation; set => m_Orientation = value; }
        public float GlyphRotation { get => m_GlyphRotation; set => m_GlyphRotation = value; }
        public bool FlipGlyphsX { get => m_FlipGlyphsX; set => m_FlipGlyphsX = value; }
        public bool FlipGlyphsY { get => m_FlipGlyphsY; set => m_FlipGlyphsY = value; }
        public bool StretchToFit { get => m_StretchToFit; set => m_StretchToFit = value; }
        public float FitRange { get => m_FitRange; set => m_FitRange = Mathf.Clamp01(value); }
        public PathOverflow Overflow { get => m_Overflow; set => m_Overflow = value; }

        public int BezierPointCount => m_BezierPoints.Count;
        public BezierPoint GetBezierPoint(int index) => m_BezierPoints[index];
        public void SetBezierPoint(int index, BezierPoint point) => m_BezierPoints[index] = point;
        public void AddBezierPoint(BezierPoint point) => m_BezierPoints.Add(point);
        public void RemoveBezierPoint(int index) => m_BezierPoints.RemoveAt(index);
        public void ClearBezierPoints() => m_BezierPoints.Clear();

        /// <summary>The sampled path, up to date with the current settings (text local space).</summary>
        public PathSampler Sampler
        {
            get
            {
                EnsurePath(ComputeHash());
                return m_Sampler;
            }
        }

        /// <summary>
        /// Setting changes are detected automatically every LateUpdate (including changes made by an Animator).
        /// Call Refresh() to apply them immediately.
        /// </summary>
        public void Refresh()
        {
            m_AppliedHash = ComputeHash();
            if (Text != null && m_Text.isActiveAndEnabled)
                m_Text.ForceMeshUpdate();
        }

        /// <summary>Converts a point from path space (before offset/rotation) to the text's local space.</summary>
        public Vector2 PathToLocal(Vector2 p)
        {
            float rad = m_PathRotation * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(p.x * c - p.y * s, p.x * s + p.y * c) + m_PathOffset;
        }

        /// <summary>Converts a point from the text's local space to path space.</summary>
        public Vector2 LocalToPath(Vector2 p)
        {
            p -= m_PathOffset;
            float rad = -m_PathRotation * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
        }

        /// <summary>Local position of the anchor point defined by Start Offset.</summary>
        public Vector2 GetAnchorLocalPosition()
        {
            PathSampler sampler = Sampler;
            sampler.Evaluate(GetAnchorDistance(sampler.Length), PathOverflow.Extrapolate, out Vector2 position, out _);
            return position;
        }

        /// <summary>Moves the anchor point to the point of the path closest to <paramref name="localPoint"/>.</summary>
        public void SetStartOffsetFromLocalPoint(Vector2 localPoint)
        {
            PathSampler sampler = Sampler;
            if (sampler.Length <= 1e-6f)
                return;
            float distance = sampler.GetClosestDistance(localPoint);
            if (m_Reverse)
                distance = sampler.Length - distance;
            m_StartOffset = m_OffsetMode == PathOffsetMode.Normalized ? distance / sampler.Length : distance;
        }

        // ---------------------------------------------------------------- Lifecycle

        void OnEnable()
        {
            m_Text = GetComponent<TMP_Text>();
            if (m_Text == null)
            {
                Debug.LogWarning($"[TextOnPath] No TextMesh Pro component found on '{name}'.", this);
                return;
            }
            if (m_BezierPoints.Count == 0)
                ResetBezier(GetDefaultUnit());

            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            m_HasBuilt = false;
            m_AppliedHash = ComputeHash() + 1; // forces a first application on the next LateUpdate
        }

        void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            // Restore the straight text.
            if (m_Text != null && m_Text.isActiveAndEnabled)
                m_Text.ForceMeshUpdate();
        }

        void LateUpdate()
        {
            if (m_Text == null)
                return;
            int hash = ComputeHash();
            if (hash != m_AppliedHash)
            {
                m_AppliedHash = hash;
                m_Text.ForceMeshUpdate(); // raises TEXT_CHANGED -> ApplyToMesh
            }
        }

        void Reset()
        {
            float u = GetDefaultUnit();
            m_Radius = 4f * u;
            m_EllipseRadii = new Vector2(6f, 3.5f) * u;
            m_Length = 12f * u;
            m_Amplitude = 1f * u;
            m_Wavelength = 6f * u;
            m_CurveHeight = 3f * u;
            m_VerticalOffset = 0f;
            ResetBezier(u);
        }

        void OnValidate()
        {
            m_Resolution = Mathf.Clamp(m_Resolution, 8, 1024);
            m_Wavelength = Mathf.Max(0.001f, m_Wavelength);
            m_SplineIndex = Mathf.Max(0, m_SplineIndex);
            if (m_Reference == this)
                m_Reference = null;
#if UNITY_EDITOR
            // In edit mode, LateUpdate only runs when something requests a player loop update.
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        void OnTextChanged(Object obj)
        {
            if (obj == m_Text && !m_IsApplying)
                ApplyToMesh();
        }

        // ---------------------------------------------------------------- Deformation

        void ApplyToMesh()
        {
            if (!isActiveAndEnabled)
                return;

            TMP_TextInfo info = m_Text.textInfo;
            if (info == null || info.characterCount == 0 || info.lineCount == 0)
                return;

            int hash = ComputeHash();
            m_AppliedHash = hash;
            EnsurePath(hash);
            float pathLength = m_Sampler.Length;
            if (m_Sampler.Count == 0)
                return;

            // Horizontal extent of the text (across all lines).
            float minX = float.MaxValue, maxX = float.MinValue;
            bool anyVisible = false;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo ci = info.characterInfo[i];
                if (!ci.isVisible)
                    continue;
                minX = Mathf.Min(minX, ci.origin);
                maxX = Mathf.Max(maxX, ci.xAdvance);
                anyVisible = true;
            }
            if (!anyVisible)
                return;

            float width = maxX - minX;
            float spacing = (m_StretchToFit && width > 1e-5f && pathLength > 1e-5f) ? pathLength * m_FitRange / width : 1f;
            float pathWidth = width * spacing;
            float alignShift = m_Alignment == PathTextAlignment.Start ? 0f
                : m_Alignment == PathTextAlignment.Center ? -pathWidth * 0.5f
                : -pathWidth;
            float direction = m_Reverse ? -1f : 1f;
            float anchor = GetAnchorDistance(pathLength);

            TMP_LineInfo firstLine = info.lineInfo[0];
            float anchorY;
            switch (m_VerticalAnchor)
            {
                case PathVerticalAnchor.Top: anchorY = firstLine.ascender; break;
                case PathVerticalAnchor.Bottom: anchorY = firstLine.descender; break;
                case PathVerticalAnchor.Middle: anchorY = (firstLine.ascender + firstLine.descender) * 0.5f; break;
                default: anchorY = firstLine.baseline; break;
            }

            float glyphRad = m_GlyphRotation * Mathf.Deg2Rad;
            float glyphCos = Mathf.Cos(glyphRad), glyphSin = Mathf.Sin(glyphRad);

            m_IsApplying = true;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo ci = info.characterInfo[i];
                if (!ci.isVisible)
                    continue;

                Vector3[] vertices = info.meshInfo[ci.materialReferenceIndex].vertices;
                int vi = ci.vertexIndex;
                float midX = (ci.origin + ci.xAdvance) * 0.5f;
                float distance = anchor + direction * (alignShift + (midX - minX) * spacing);

                bool onPath = m_Sampler.Evaluate(distance, m_Overflow, out Vector2 point, out Vector2 tangent);
                if (!onPath)
                {
                    for (int k = 0; k < 4; k++)
                        vertices[vi + k] = new Vector3(point.x, point.y, vertices[vi + k].z);
                    continue;
                }

                tangent *= direction;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                Vector2 origin = point + normal * (ci.baseLine - anchorY + m_VerticalOffset);

                Vector2 axisX, axisY;
                if (m_Orientation == PathGlyphOrientation.FollowPath)
                {
                    axisX = tangent;
                    axisY = normal;
                }
                else
                {
                    axisX = Vector2.right;
                    axisY = Vector2.up;
                }
                if (glyphRad != 0f)
                {
                    Vector2 rx = axisX * glyphCos + axisY * glyphSin;
                    axisY = axisY * glyphCos - axisX * glyphSin;
                    axisX = rx;
                }

                float centerY = (ci.ascender + ci.descender) * 0.5f;
                for (int k = 0; k < 4; k++)
                {
                    Vector3 v = vertices[vi + k];
                    float lx = v.x - midX;
                    float ly = v.y;
                    if (m_FlipGlyphsX)
                        lx = -lx;
                    if (m_FlipGlyphsY)
                        ly = 2f * centerY - ly;
                    ly -= ci.baseLine;

                    Vector2 p = origin + axisX * lx + axisY * ly;
                    vertices[vi + k] = new Vector3(p.x, p.y, v.z);
                }
            }

            m_Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            if (m_Text is TextMeshPro)
            {
                for (int i = 0; i < info.materialCount; i++)
                {
                    Mesh mesh = info.meshInfo[i].mesh;
                    if (mesh != null)
                        mesh.RecalculateBounds();
                }
            }
            m_IsApplying = false;
        }

        float GetAnchorDistance(float pathLength)
        {
            float distance = m_OffsetMode == PathOffsetMode.Normalized ? m_StartOffset * pathLength : m_StartOffset;
            return m_Reverse ? pathLength - distance : distance;
        }

        // ---------------------------------------------------------------- Path building

        void EnsurePath(int hash)
        {
            if (m_HasBuilt && hash == m_BuiltHash)
                return;
            BuildPath();
            m_BuiltHash = hash;
            m_HasBuilt = true;
        }

        void BuildPath()
        {
            m_Buffer.Clear();
            bool closed = false;
            bool externalSpace = false;
            int res = Mathf.Clamp(m_Resolution, 8, 1024);
            float dir = m_Clockwise ? -1f : 1f;

            switch (m_Shape)
            {
                case PathShape.Circle:
                    AddEllipse(new Vector2(m_Radius, m_Radius), res, dir);
                    closed = true;
                    break;

                case PathShape.Ellipse:
                    AddEllipse(m_EllipseRadii, res, dir);
                    closed = true;
                    break;

                case PathShape.Arc:
                    for (int i = 0; i <= res; i++)
                    {
                        float a = (m_StartAngle + dir * m_SweepAngle * i / res) * Mathf.Deg2Rad;
                        m_Buffer.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * m_Radius);
                    }
                    break;

                case PathShape.Line:
                    m_Buffer.Add(new Vector2(-m_Length * 0.5f, 0f));
                    m_Buffer.Add(new Vector2(m_Length * 0.5f, 0f));
                    break;

                case PathShape.Wave:
                {
                    float phase = m_Phase * Mathf.Deg2Rad;
                    float k = 2f * Mathf.PI / Mathf.Max(0.001f, m_Wavelength);
                    for (int i = 0; i <= res; i++)
                    {
                        float x = Mathf.Lerp(-m_Length * 0.5f, m_Length * 0.5f, (float)i / res);
                        m_Buffer.Add(new Vector2(x, m_Amplitude * Mathf.Sin(k * x + phase)));
                    }
                    break;
                }

                case PathShape.Curve:
                    for (int i = 0; i <= res; i++)
                    {
                        float t = (float)i / res;
                        float y = m_Curve != null ? m_Curve.Evaluate(t) : 0f;
                        m_Buffer.Add(new Vector2(Mathf.Lerp(-m_Length * 0.5f, m_Length * 0.5f, t), y * m_CurveHeight));
                    }
                    break;

                case PathShape.Bezier:
                    AddBezier(res);
                    closed = m_ClosedBezier;
                    break;

                case PathShape.Reference:
                    closed = AddReferencePoints();
                    externalSpace = true;
                    break;

                case PathShape.Spline:
                    closed = AddSplinePoints(res);
                    externalSpace = true;
                    break;
            }

            // External paths are already in local space; offset/rotation are applied on top of them.
            if (!externalSpace || m_PathOffset != Vector2.zero || m_PathRotation != 0f)
            {
                for (int i = 0; i < m_Buffer.Count; i++)
                    m_Buffer[i] = PathToLocal(m_Buffer[i]);
            }

            m_Sampler.Build(m_Buffer, closed);
        }

        void AddEllipse(Vector2 radii, int res, float dir)
        {
            for (int i = 0; i < res; i++)
            {
                float a = (m_StartAngle + dir * 360f * i / res) * Mathf.Deg2Rad;
                m_Buffer.Add(new Vector2(Mathf.Cos(a) * radii.x, Mathf.Sin(a) * radii.y));
            }
        }

        void AddBezier(int res)
        {
            int n = m_BezierPoints.Count;
            if (n == 0)
                return;
            if (n == 1)
            {
                m_Buffer.Add(m_BezierPoints[0].position);
                return;
            }

            bool closed = m_ClosedBezier;
            int segmentCount = closed ? n : n - 1;
            int perSegment = Mathf.Max(8, res / segmentCount);
            for (int s = 0; s < segmentCount; s++)
            {
                BezierPoint a = m_BezierPoints[s];
                BezierPoint b = m_BezierPoints[(s + 1) % n];
                Vector2 p0 = a.position;
                Vector2 p1 = a.position + a.outTangent;
                Vector2 p2 = b.position + b.inTangent;
                Vector2 p3 = b.position;
                for (int i = 0; i < perSegment; i++)
                {
                    float t = (float)i / perSegment;
                    float u = 1f - t;
                    m_Buffer.Add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
                }
            }
            if (!closed)
                m_Buffer.Add(m_BezierPoints[n - 1].position);
        }

        /// <summary>Copies the referenced text's path, converted into this text's local space.</summary>
        bool AddReferencePoints()
        {
            if (m_Reference == null || m_Reference == this || s_ReferenceDepth >= k_MaxReferenceDepth)
                return false;

            s_ReferenceDepth++;
            try
            {
                PathSampler source = m_Reference.Sampler;
                Matrix4x4 toLocal = transform.worldToLocalMatrix * m_Reference.transform.localToWorldMatrix;
                int count = source.IsClosed ? source.Count - 1 : source.Count;
                for (int i = 0; i < count; i++)
                    m_Buffer.Add(toLocal.MultiplyPoint3x4(source.GetPoint(i)));
                return source.IsClosed;
            }
            finally
            {
                s_ReferenceDepth--;
            }
        }

        /// <summary>Samples a Unity Splines spline, converted into this text's local space (projected on local XY).</summary>
        bool AddSplinePoints(int res)
        {
#if TEXTONPATH_SPLINES
            var container = m_SplineContainer as SplineContainer;
            if (container == null || container.Splines.Count == 0)
                return false;

            Spline spline = container.Splines[Mathf.Clamp(m_SplineIndex, 0, container.Splines.Count - 1)];
            if (spline.Count == 0)
                return false;

            Matrix4x4 toLocal = transform.worldToLocalMatrix * container.transform.localToWorldMatrix;
            bool closed = spline.Closed;
            int count = closed ? res : res + 1;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = SplineUtility.EvaluatePosition(spline, (float)i / res);
                m_Buffer.Add(toLocal.MultiplyPoint3x4(p));
            }
            return closed;
#else
            return false;
#endif
        }

        void ResetBezier(float u)
        {
            m_BezierPoints.Clear();
            m_BezierPoints.Add(new BezierPoint(new Vector2(-6f, 0f) * u, new Vector2(2f, 4f) * u));
            m_BezierPoints.Add(new BezierPoint(new Vector2(6f, 0f) * u, new Vector2(2f, 4f) * u));
        }

        /// <summary>Size of one "em" in the text's local space (used for default values).</summary>
        float GetDefaultUnit()
        {
            TMP_Text text = GetComponent<TMP_Text>();
            if (text == null)
                return 1f;
            // TextMeshPro 3D: 1 font point = 0.1 unit; UGUI: 1 font point = 1 unit.
            return text.fontSize * (text is TextMeshPro ? 0.1f : 1f);
        }

        int ComputeHash()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (int)m_Shape;
                h = h * 31 + m_PathOffset.GetHashCode();
                h = h * 31 + m_PathRotation.GetHashCode();
                h = h * 31 + m_Resolution;
                h = h * 31 + (int)m_OffsetMode;
                h = h * 31 + m_StartOffset.GetHashCode();
                h = h * 31 + (int)m_Alignment;
                h = h * 31 + (int)m_VerticalAnchor;
                h = h * 31 + m_VerticalOffset.GetHashCode();
                h = h * 31 + (int)m_Orientation;
                h = h * 31 + m_GlyphRotation.GetHashCode();
                h = h * 31 + (int)m_Overflow;
                h = h * 31 + m_FitRange.GetHashCode();
                h = h * 31 + (m_Reverse ? 1 : 0) + (m_FlipGlyphsX ? 2 : 0) + (m_FlipGlyphsY ? 4 : 0) + (m_StretchToFit ? 8 : 0);

                switch (m_Shape)
                {
                    case PathShape.Circle:
                    case PathShape.Ellipse:
                    case PathShape.Arc:
                        h = h * 31 + m_Radius.GetHashCode();
                        h = h * 31 + m_EllipseRadii.GetHashCode();
                        h = h * 31 + m_StartAngle.GetHashCode();
                        h = h * 31 + m_SweepAngle.GetHashCode();
                        h = h * 31 + (m_Clockwise ? 1 : 0);
                        break;

                    case PathShape.Line:
                    case PathShape.Wave:
                        h = h * 31 + m_Length.GetHashCode();
                        h = h * 31 + m_Amplitude.GetHashCode();
                        h = h * 31 + m_Wavelength.GetHashCode();
                        h = h * 31 + m_Phase.GetHashCode();
                        break;

                    case PathShape.Curve:
                        h = h * 31 + m_Length.GetHashCode();
                        h = h * 31 + m_CurveHeight.GetHashCode();
                        if (m_Curve != null)
                        {
                            for (int i = 0; i < m_Curve.length; i++)
                            {
                                Keyframe key = m_Curve[i];
                                h = h * 31 + key.time.GetHashCode();
                                h = h * 31 + key.value.GetHashCode();
                                h = h * 31 + key.inTangent.GetHashCode();
                                h = h * 31 + key.outTangent.GetHashCode();
                            }
                        }
                        break;

                    case PathShape.Bezier:
                        h = h * 31 + (m_ClosedBezier ? 1 : 0);
                        for (int i = 0; i < m_BezierPoints.Count; i++)
                        {
                            BezierPoint p = m_BezierPoints[i];
                            h = h * 31 + p.position.GetHashCode();
                            h = h * 31 + p.inTangent.GetHashCode();
                            h = h * 31 + p.outTangent.GetHashCode();
                        }
                        break;

                    case PathShape.Reference:
                        if (m_Reference != null && m_Reference != this && s_ReferenceDepth < k_MaxReferenceDepth)
                        {
                            s_ReferenceDepth++;
                            h = h * 31 + m_Reference.ComputeHash();
                            s_ReferenceDepth--;
                            h = h * 31 + (transform.worldToLocalMatrix * m_Reference.transform.localToWorldMatrix).GetHashCode();
                        }
                        break;

                    case PathShape.Spline:
                        h = h * 31 + ComputeSplineHash();
                        break;
                }
                return h;
            }
        }

        int ComputeSplineHash()
        {
#if TEXTONPATH_SPLINES
            var container = m_SplineContainer as SplineContainer;
            if (container == null || container.Splines.Count == 0)
                return 0;
            unchecked
            {
                Spline spline = container.Splines[Mathf.Clamp(m_SplineIndex, 0, container.Splines.Count - 1)];
                int h = (transform.worldToLocalMatrix * container.transform.localToWorldMatrix).GetHashCode();
                h = h * 31 + m_SplineIndex;
                h = h * 31 + (spline.Closed ? 1 : 0);
                for (int i = 0; i < spline.Count; i++)
                {
                    BezierKnot knot = spline[i];
                    h = h * 31 + knot.Position.GetHashCode();
                    h = h * 31 + knot.TangentIn.GetHashCode();
                    h = h * 31 + knot.TangentOut.GetHashCode();
                    h = h * 31 + knot.Rotation.GetHashCode();
                }
                return h;
            }
#else
            return 0;
#endif
        }

        // ---------------------------------------------------------------- Gizmos

        void OnDrawGizmosSelected()
        {
            if (!m_ShowPathGizmo)
                return;
            PathSampler sampler = Sampler;
            if (sampler.Count < 2)
                return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            for (int i = 1; i < sampler.Count; i++)
                Gizmos.DrawLine(sampler.GetPoint(i - 1), sampler.GetPoint(i));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
