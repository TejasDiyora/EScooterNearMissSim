using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// Small helpers for building the scene out of Unity's primitive shapes.
    /// The shapes are for looks only: all movement and detection is done with the
    /// simulation's own geometry, so their colliders are removed.
    /// </summary>
    public static class Shapes
    {
        public static GameObject Create(Transform parent, PrimitiveType shape, string name,
            Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;

            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null) Object.Destroy(partCollider);

            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Renderer partRenderer = part.GetComponent<Renderer>();
            if (partRenderer != null) partRenderer.sharedMaterial = MaterialLibrary.Get(color);
            return part;
        }

        /// <summary>A box given by its centre and full size.</summary>
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Color color)
        {
            return Create(parent, PrimitiveType.Cube, name, center, size, color);
        }

        /// <summary>
        /// A flat rectangle lying on the ground, such as a lane line.
        /// <paramref name="length"/> runs along the heading and <paramref name="width"/> across it.
        /// </summary>
        public static GameObject Strip(Transform parent, string name, Vector3 center, Heading along,
            float length, float width, float height, Color color)
        {
            bool alongX = along == Heading.East || along == Heading.West;
            Vector3 size = alongX
                ? new Vector3(length, height, width)
                : new Vector3(width, height, length);
            Vector3 position = new Vector3(center.x, height * 0.5f, center.z);
            return Create(parent, PrimitiveType.Cube, name, position, size, color);
        }
    }
}
