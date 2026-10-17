using Cinderbird.Core;
using UnityEngine;

namespace Cinderbird.Nest
{
    /// <summary>
    /// The phoenix's silhouette as a flat mesh, wings raised and tail streaming down, facing +z: the flames are emitted from
    /// its surface, so the bird takes shape out of the fire. About 1.9 m across the wings and 1.3 m tall at scale 1, with
    /// the body's middle at the origin.
    /// </summary>
    public static class PhoenixShape
    {
        // The right half of the outline, from the top of the head down to the tip of the tail; the left half mirrors it.
        private static readonly Vector2[] RightHalf =
        {
            new Vector2(0f, 0.6f), new Vector2(0.07f, 0.52f), new Vector2(0.09f, 0.38f), new Vector2(0.3f, 0.42f),
            new Vector2(0.55f, 0.62f), new Vector2(0.8f, 0.86f), new Vector2(0.74f, 0.66f), new Vector2(0.93f, 0.7f),
            new Vector2(0.8f, 0.5f), new Vector2(0.95f, 0.47f), new Vector2(0.75f, 0.32f), new Vector2(0.86f, 0.23f),
            new Vector2(0.55f, 0.14f), new Vector2(0.3f, 0.05f), new Vector2(0.15f, -0.1f), new Vector2(0.22f, -0.42f),
            new Vector2(0.1f, -0.3f), new Vector2(0.12f, -0.66f), new Vector2(0f, -0.42f),
        };

        // The fan's middle: the body, from where every edge of the outline is in view.
        private static readonly Vector2 Middle = new Vector2(0f, 0.15f);

        public static MeshData Build()
        {
            // The outline: right half top to bottom, then the left half bottom to top (without repeating the ends).
            int half = RightHalf.Length;
            int outlineCount = half + half - 2;
            Vector3[] vertices = new Vector3[outlineCount + 1];
            vertices[0] = new Vector3(Middle.x, Middle.y, 0f);
            for (int i = 0; i < half; i++)
            {
                vertices[1 + i] = new Vector3(RightHalf[i].x, RightHalf[i].y, 0f);
            }

            for (int i = half - 2; i >= 1; i--)
            {
                vertices[1 + half + (half - 2 - i)] = new Vector3(-RightHalf[i].x, RightHalf[i].y, 0f);
            }

            int[] triangles = new int[outlineCount * 3];
            for (int i = 0; i < outlineCount; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + i;
                triangles[i * 3 + 2] = 1 + (i + 1) % outlineCount;
            }

            return new MeshData(vertices, triangles);
        }
    }
}
