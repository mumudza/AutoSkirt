using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics;
using VRC.SDK3.Dynamics.Contact;
using VRC.SDK3.Dynamics.Constraint;


namespace AutoSkirt.Editor.Utils
{
    public static class AimComponentUtility
    {
        /// <summary>
        /// Adds a simple aim constraint the specified GameObject to a specific set of sources.
        /// </summary>
        public static VRCAimConstraint AddAimConstraint(GameObject colGO, VRC.Dynamics.VRCConstraintSourceKeyableList newSources)
        {
            var aim = colGO.AddComponent<VRCAimConstraint>();

            aim.Sources = newSources;

            // Configure the aim constraint
            aim.AimAxis = Vector3.up;
            aim.UpAxis = Vector3.forward;
            aim.WorldUp = VRC.Dynamics.VRCConstraintBase.WorldUpType.SceneUp;
            aim.WorldUpVector = Vector3.up;
            aim.WorldUpTransform = null;

            aim.RotationAtRest = Vector3.zero;
            aim.RotationOffset = Vector3.zero;

            aim.AffectsRotationX = true;
            aim.AffectsRotationY = true;
            aim.AffectsRotationZ = true;

            aim.GlobalWeight = 1f;
            aim.Locked = true;
            aim.IsActive = true;

            return aim;
        }
    }
}
