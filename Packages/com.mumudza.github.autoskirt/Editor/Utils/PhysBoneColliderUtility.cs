using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace AutoSkirt.Editor.Utils
{
    public static class PhysBoneColliderUtility
    {
        /// <summary>
        /// Adds a simple capsule PhysBone collider to the specified GameObject.
        /// </summary>
        public static VRCPhysBoneCollider AddCapsuleCollider(GameObject colGO, float radius, float height)
        {
            var physCol = colGO.AddComponent<VRCPhysBoneCollider>();
            physCol.shapeType = VRCPhysBoneCollider.ShapeType.Capsule;
            physCol.radius = radius;
            physCol.height = height;

            // Adjust position so the capsule is centered vertically
            physCol.position = new Vector3(0, height * 0.5f - radius, 0);

            return physCol;
        }

        /// <summary>
        /// Computes the capsule radius needed to enclose all vertices influenced by a leg bone,
        /// between the leg and knee joint, by projecting them into a space where the leg-knee axis is vertical.
        /// </summary>
        public static float CalculateCapsuleRadiusFromLegInfluence(
            SkinnedMeshRenderer meshRenderer,
            Transform legBone,
            Transform kneeBone,
            float weightThreshold = 0.8f)
        {
            if (meshRenderer == null || legBone == null || kneeBone == null || meshRenderer.sharedMesh == null)
            {
                Debug.LogWarning("Invalid inputs for radius calculation.");
                return 0f;
            }

            var mesh = meshRenderer.sharedMesh;
            var vertices = mesh.vertices;
            var boneWeights = mesh.boneWeights;
            var bones = meshRenderer.bones;

            int legBoneIndex = System.Array.IndexOf(bones, legBone);
            if (legBoneIndex < 0)
            {
                Debug.LogWarning("Leg bone not found in SkinnedMeshRenderer.");
                return 0f;
            }

            // Get leg-to-knee direction and length in world space
            Vector3 legPos = legBone.position;
            Vector3 kneePos = kneeBone.position;
            Vector3 legToKneeVec = kneePos - legPos;
            float legLength = legToKneeVec.magnitude;

            if (legLength < 0.001f)
            {
                Debug.LogWarning("Leg length too small to compute alignment.");
                return 0f;
            }

            Vector3 legToKneeDir = legToKneeVec.normalized;

            // Rotation that aligns leg→knee with Ve    ctor3.up
            Quaternion alignToYAxis = Quaternion.FromToRotation(legToKneeDir, Vector3.up);

            // Skinned mesh transform
            Matrix4x4 meshToWorld = meshRenderer.transform.localToWorldMatrix;

            float maxDistance = 0f;
            int includedCount = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                float weight = GetWeightForBone(boneWeights[i], legBoneIndex);
                if (weight < weightThreshold)
                    continue;

                // Convert to world space
                Vector3 vertexWorld = meshToWorld.MultiplyPoint3x4(vertices[i]);

                // Shift relative to leg origin
                Vector3 relativeToLeg = vertexWorld - legPos;

                // Rotate so leg-knee axis is vertical
                Vector3 aligned = alignToYAxis * relativeToLeg;

                // Only include points between 0 and legLength along the Y-axis
                if (aligned.y < 0f || aligned.y > legLength)
                    continue;

                float radius = new Vector2(aligned.x, aligned.z).magnitude;
                if (radius > maxDistance)
                    maxDistance = radius;

                includedCount++;
            }

            Debug.Log($"[AutoSkirt] Included {includedCount} vertices for {legBone.name}. Max radius: {maxDistance:F4}");

            return maxDistance;
        }


        private static float GetWeightForBone(BoneWeight bw, int boneIndex)
        {
            if (bw.boneIndex0 == boneIndex) return bw.weight0;
            if (bw.boneIndex1 == boneIndex) return bw.weight1;
            if (bw.boneIndex2 == boneIndex) return bw.weight2;
            if (bw.boneIndex3 == boneIndex) return bw.weight3;
            return 0f;
        }
    }
}
