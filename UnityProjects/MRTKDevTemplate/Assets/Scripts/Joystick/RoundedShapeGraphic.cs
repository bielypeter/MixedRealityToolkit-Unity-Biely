// Disable "missing XML comment" warning for samples. While nice to have, this XML documentation is not required for samples.
#pragma warning disable CS1591

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MixedReality.Toolkit.Examples.Demos
{
    /// <summary>
    /// Procedural uGUI shape: a rounded rectangle, pill or circle, either filled or drawn as an outline.
    /// Edges are anti-aliased with a small alpha feather, so it stays smooth at any size and needs no sprite.
    /// </summary>
    /// <remarks>
    /// Set <see cref="CornerRadius"/> to a large value (or enable <see cref="Circle"/>) to get a circle.
    /// </remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("MRTK/Examples/Rounded Shape Graphic")]
    public class RoundedShapeGraphic : MaskableGraphic
    {
        [SerializeField]
        [Tooltip("Draw a perfect circle that fits the smaller side of the rect. Overrides the corner radius.")]
        private bool circle = false;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Corner radius in canvas units. Clamped to half of the smaller side.")]
        private float cornerRadius = 8f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Outline thickness in canvas units. 0 draws a filled shape.")]
        private float outlineThickness = 0f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Width of the anti-aliasing feather in canvas units.")]
        private float edgeSoftness = 0.6f;

        [SerializeField]
        [Range(2, 64)]
        [Tooltip("Number of segments used to tessellate each corner.")]
        private int cornerSegments = 16;

        public bool Circle
        {
            get => circle;
            set { circle = value; SetVerticesDirty(); }
        }

        public float CornerRadius
        {
            get => cornerRadius;
            set { cornerRadius = Mathf.Max(0f, value); SetVerticesDirty(); }
        }

        public float OutlineThickness
        {
            get => outlineThickness;
            set { outlineThickness = Mathf.Max(0f, value); SetVerticesDirty(); }
        }

        public float EdgeSoftness
        {
            get => edgeSoftness;
            set { edgeSoftness = Mathf.Max(0f, value); SetVerticesDirty(); }
        }

        private readonly List<Vector2> corners = new List<Vector2>(4);
        private readonly List<Vector2> directions = new List<Vector2>(128);
        private readonly List<int> cornerIndex = new List<int>(128);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            float halfMin = Mathf.Min(rect.width, rect.height) * 0.5f;
            float radius = circle ? halfMin : Mathf.Min(cornerRadius, halfMin);

            if (circle)
            {
                // Square the rect around its center so the circle is round in non-square rects.
                Vector2 c = rect.center;
                rect = new Rect(c.x - halfMin, c.y - halfMin, halfMin * 2f, halfMin * 2f);
            }

            BuildOutline(rect, radius);

            float feather = edgeSoftness * 0.5f;
            Color32 solid = color;
            Color32 clear = color;
            clear.a = 0;

            if (outlineThickness <= 0f || outlineThickness >= halfMin)
            {
                // Filled: feather band around the edge plus a fan towards the center.
                int outerStart = AddRing(vh, rect, radius, -feather, clear);
                int innerStart = AddRing(vh, rect, radius, feather, solid);
                Stitch(vh, outerStart, innerStart);

                int center = vh.currentVertCount;
                vh.AddVert(rect.center, solid, Vector2.zero);
                int count = directions.Count;
                for (int i = 0; i < count; i++)
                {
                    vh.AddTriangle(center, innerStart + i, innerStart + (i + 1) % count);
                }
            }
            else
            {
                // Outline: feather out, solid band, feather in.
                int a = AddRing(vh, rect, radius, -feather, clear);
                int b = AddRing(vh, rect, radius, feather, solid);
                int c = AddRing(vh, rect, radius, outlineThickness - feather, solid);
                int d = AddRing(vh, rect, radius, outlineThickness + feather, clear);
                Stitch(vh, a, b);
                Stitch(vh, b, c);
                Stitch(vh, c, d);
            }
        }

        /// <summary>
        /// Precomputes the corner centers and the outward direction of every perimeter vertex.
        /// </summary>
        private void BuildOutline(Rect rect, float radius)
        {
            corners.Clear();
            directions.Clear();
            cornerIndex.Clear();

            // Corner centers, counter-clockwise starting at top-right.
            corners.Add(new Vector2(rect.xMax - radius, rect.yMax - radius));
            corners.Add(new Vector2(rect.xMin + radius, rect.yMax - radius));
            corners.Add(new Vector2(rect.xMin + radius, rect.yMin + radius));
            corners.Add(new Vector2(rect.xMax - radius, rect.yMin + radius));

            int segments = Mathf.Max(2, cornerSegments);
            for (int corner = 0; corner < 4; corner++)
            {
                float start = corner * 90f;
                for (int s = 0; s <= segments; s++)
                {
                    float angle = (start + 90f * s / segments) * Mathf.Deg2Rad;
                    directions.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                    cornerIndex.Add(corner);
                }
            }
        }

        /// <summary>
        /// Adds one ring of perimeter vertices, inset from the outer edge by <paramref name="inset"/>.
        /// Negative insets grow the shape outwards (used for the outer feather).
        /// </summary>
        private int AddRing(VertexHelper vh, Rect rect, float radius, float inset, Color32 vertexColor)
        {
            int start = vh.currentVertCount;
            float r = radius - inset;
            Vector2 center = rect.center;
            for (int i = 0; i < directions.Count; i++)
            {
                Vector2 point;
                if (r >= 0f)
                {
                    point = corners[cornerIndex[i]] + directions[i] * r;
                }
                else
                {
                    // Inset deeper than the corner radius: shrink the corner center towards the middle instead.
                    Vector2 cornerCenter = corners[cornerIndex[i]];
                    Vector2 toCenter = center - cornerCenter;
                    Vector2 shift = new Vector2(Mathf.Sign(toCenter.x), Mathf.Sign(toCenter.y)) * -r;
                    point = cornerCenter + Vector2.Min(Vector2.Max(shift, -Abs(toCenter)), Abs(toCenter));
                }
                vh.AddVert(point, vertexColor, Vector2.zero);
            }
            return start;
        }

        private void Stitch(VertexHelper vh, int outerStart, int innerStart)
        {
            int count = directions.Count;
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                vh.AddTriangle(outerStart + i, outerStart + next, innerStart + next);
                vh.AddTriangle(outerStart + i, innerStart + next, innerStart + i);
            }
        }

        private static Vector2 Abs(Vector2 v) => new Vector2(Mathf.Abs(v.x), Mathf.Abs(v.y));

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
#pragma warning restore CS1591
