using UnityEngine;

namespace VoxelEngine
{
    /// <summary>
    /// Class with helper functions for math.
    /// </summary>
    public class MathHelper
    {
        /// <summary>
        /// Function to check intersection between AABB and Triangle.
        /// Adapted from https://stackoverflow.com/a/17503268 which is based on https://fileadmin.cs.lth.se/cs/Personal/Tomas_Akenine-Moller/code/tribox_tam.pdf
        /// and can also be found in section 22.12 of Real-time Rendering 4th edition.
        /// </summary>
        public static bool CheckAABBAndTriangleIntersection(Vector3 boxStart, Vector3 boxEnd, Vector3[] triangleVertices, Vector3[] boxVertices,
            Vector3 triangleNormal)
        {
            double triangleMin, triangleMax;
            double boxMin, boxMax;

            // Test the box normals (x-, y- and z-axes)
            Vector3[] boxNormals = new Vector3[] { Vector3.right, Vector3.up, Vector3.forward };

            for (int i = 0; i < 3; i++)
            {
                Vector3 n = boxNormals[i];
                Project(triangleVertices, boxNormals[i], out triangleMin, out triangleMax);
                if (triangleMax < boxStart[i] || triangleMin > boxEnd[i])
                    return false; // No intersection possible.
            }

            // Test the triangle normal
            double triangleOffset = Vector3.Dot(triangleNormal, triangleVertices[0]);
            Project(boxVertices, triangleNormal, out boxMin, out boxMax);
            if (boxMax < triangleOffset || boxMin > triangleOffset)
                return false; // No intersection possible.

            // Test the nine edge cross-products
            Vector3[] triangleEdges = new Vector3[] {
            triangleVertices[0] - triangleVertices[1],
            triangleVertices[1] - triangleVertices[2],
            triangleVertices[2] - triangleVertices[0]
        };

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    // The box normals are the same as it's edge tangents
                    Vector3 axis = Vector3.Cross(triangleEdges[i], boxNormals[j]);
                    Project(boxVertices, axis, out boxMin, out boxMax);
                    Project(triangleVertices, axis, out triangleMin, out triangleMax);
                    if (boxMax < triangleMin || boxMin > triangleMax)
                        return false; // No intersection possible
                }

            // No separating axis found.
            return true;
        }

        private static void Project(Vector3[] points, Vector3 axis,
                out double min, out double max)
        {
            min = double.PositiveInfinity;
            max = double.NegativeInfinity;
            foreach (Vector3 point in points)
            {
                double val = Vector3.Dot(axis, point);
                if (val < min) min = val;
                if (val > max) max = val;
            }
        }
    }
}