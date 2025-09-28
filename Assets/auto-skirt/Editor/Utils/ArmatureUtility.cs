using System.Collections.Generic;
using UnityEngine;

namespace AutoSkirt.Editor.Utils
{
    public static class ArmatureUtility
    {
        /// <summary>
        /// Gets the bottom children of the specified bones.
        /// </summary>
        public static List<Transform> GetBottomChildren(List<GameObject> bones)
        {
            var bottomChildren = new List<Transform>();

            foreach (var bone in bones)
            {
                if (bone == null)
                    continue;

                var leaves = GetLeafBones(bone.transform);
                bottomChildren.AddRange(leaves);
            }

            return bottomChildren;
        }

        private static List<Transform> GetLeafBones(Transform root)
        {
            var leafList = new List<Transform>();

            void Traverse(Transform t)
            {
                if (t.childCount == 0)
                {
                    leafList.Add(t);
                    return;
                }

                foreach (Transform child in t)
                {
                    Traverse(child);
                }
            }

            Traverse(root);
            return leafList;
        }
    }
}
