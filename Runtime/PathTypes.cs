using System;
using UnityEngine;

namespace TMPTextOnPath
{
    /// <summary>Where the path followed by the text comes from.</summary>
    public enum PathShape
    {
        Circle,
        Ellipse,
        Arc,
        Line,
        Wave,
        /// <summary>Free profile driven by an AnimationCurve.</summary>
        Curve,
        /// <summary>Cubic Bezier spline edited in the Scene view.</summary>
        Bezier,
        /// <summary>Uses the path of another TextOnPath, so several texts can share one path.</summary>
        Reference,
        /// <summary>Uses a SplineContainer from the Unity Splines package (com.unity.splines).</summary>
        Spline,
    }

    /// <summary>Where the anchor point (Start Offset) sits relative to the text.</summary>
    public enum PathTextAlignment
    {
        Start,
        Center,
        End,
    }

    /// <summary>Which part of the text lies on the path.</summary>
    public enum PathVerticalAnchor
    {
        Baseline,
        Middle,
        Top,
        Bottom,
    }

    /// <summary>How glyphs are oriented along the path.</summary>
    public enum PathGlyphOrientation
    {
        /// <summary>Each glyph is rotated to match the path tangent.</summary>
        FollowPath,
        /// <summary>Glyphs stay upright; only their position follows the path.</summary>
        Upright,
    }

    /// <summary>Unit of the Start Offset value.</summary>
    public enum PathOffsetMode
    {
        /// <summary>0 = start of the path, 1 = end of the path.</summary>
        Normalized,
        /// <summary>Distance along the path, in the text's local units.</summary>
        Distance,
    }

    /// <summary>What happens to characters that run past the ends of an open path.</summary>
    public enum PathOverflow
    {
        /// <summary>Continue in a straight line along the end tangents.</summary>
        Extrapolate,
        /// <summary>Hide characters that are off the path.</summary>
        Hide,
    }

    /// <summary>Control point of a cubic Bezier spline. Tangents are relative to the position.</summary>
    [Serializable]
    public struct BezierPoint
    {
        public Vector2 position;
        public Vector2 inTangent;
        public Vector2 outTangent;
        [Tooltip("When enabled, the in and out tangents can be edited independently.")]
        public bool broken;

        public BezierPoint(Vector2 position, Vector2 outTangent)
        {
            this.position = position;
            this.outTangent = outTangent;
            inTangent = -outTangent;
            broken = false;
        }
    }
}
