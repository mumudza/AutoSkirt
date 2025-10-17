using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;

using System.Collections.Generic;
using System.Linq;

using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics;
using VRC.SDK3.Dynamics.Contact;
using VRC.SDK3.Dynamics.Constraint;

using AutoSkirt.Editor.Utils;

public class AutoskirtEditor : EditorWindow
{
    private GameObject avatarRoot;
    private bool autoCreateParent = false;
    private float verticalPositionMultiplier = 0.1f;
    private Transform manualParent;


    private bool enableXBoneConfig = false;
    private bool enableTBoneConfig = false;
    private bool enableLegBoneConfig = false;
    private bool enableRotationConstraint = false;
    private float rotationConstraintWeight = 0.5f;


    // Auto skirt parent creation
    private List<GameObject> skirtBones = new List<GameObject>();
    private Vector2 skirtBoneListScroll;


    // Skirt bones
    private float collisionRadiusMultiplier = 0.8f;

    // Phys bone colliders
    private float tColliderRadius = 0.05f;
    private float xColliderRadius = 0.05f;
    private float tColliderLengthMultiplier = 0.7f;
    private float legColliderRadius = 0.06f;

    // Phys bones
    private bool replaceCreatePhysBone = false;
    private bool autoReplaceColliders = false;

    // Knee/Leg game objects
    private Transform leftLeg;
    private Transform leftKnee;
    private Transform rightLeg;
    private Transform rightKnee;

    // Skinned mesh autogen
    private SkinnedMeshRenderer skinnedMesh;
    private float xColliderAutoRadiusMultiplier = 0.8f;
    private float tColliderAutoRadiusMultiplier = 0.8f;
    private float legColliderAutoRadiusMultiplier = 1.0f;
    private float vertexWeightThreshold = 0.8f;



    [MenuItem("Tools/Autoskirt Tool")]
    public static void ShowWindow()
    {
        GetWindow<AutoskirtEditor>("Autoskirt");
    }

    private void OnGUI()
    {
        GUILayout.Label("Autoskirt Setup Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        GUIStyle wrappedLabelStyle = new GUIStyle(EditorStyles.label);
        wrappedLabelStyle.wordWrap = true;

        EditorGUILayout.LabelField("BUGS:" +
            "\nIf the skirt element rotates when entering play mode, I suggest locking the parent constraint on the skirt parent root bone outside play mode." +
            "\nI believe this is due to some mistakes on my side when exporting some MMD models, but I'd prefer not to make any assumptions here.",
            wrappedLabelStyle);

        EditorGUILayout.Space();

        avatarRoot = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Avatar Root", "The root GameObject of your VRChat avatar."),
            avatarRoot,
            typeof(GameObject),
            true
        );

        EditorGUILayout.Space();
        DrawParentingModeSection();
        EditorGUILayout.Space();
        DrawLegBoneSelection();
        EditorGUILayout.Space();
        DrawTechniqueTogglesSection();
        EditorGUILayout.Space();
        DrawColliderAutoFillSection();
        EditorGUILayout.Space();
        DrawBoneCreationSection();
        EditorGUILayout.Space();

        if (GUILayout.Button("Apply Configuration"))
        {
            ApplyConfiguration();
        }
    }

    private void DrawColliderAutoFillSection()
    {
        GUILayout.Label("Auto-Fill Collider Radius (Minimum radius to fill entire leg vertices)", EditorStyles.boldLabel);

        // SkinnedMeshRenderer selector
        skinnedMesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
            "Skinned Mesh", 
            skinnedMesh, 
            typeof(SkinnedMeshRenderer), 
            true // allow scene objects
        );

        if (skinnedMesh)
        {
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 280f;
            xColliderAutoRadiusMultiplier = EditorGUILayout.FloatField(
                new GUIContent("X Bone Colliders Radius Multiplier", "Radius = Multiplier * Minimum collider radius to fill entire leg with capsule."),
                xColliderAutoRadiusMultiplier
            );
            tColliderAutoRadiusMultiplier = EditorGUILayout.FloatField(
                new GUIContent("X Bone Colliders Radius Multiplier", "Radius = Multiplier * Minimum collider radius to fill entire leg with capsule."),
                tColliderAutoRadiusMultiplier
            );
            legColliderAutoRadiusMultiplier = EditorGUILayout.FloatField(
                new GUIContent("Leg Bone Colliders Radius Multiplier", "Radius = Multiplier * Minimum collider radius to fill entire leg with capsule."),
                legColliderAutoRadiusMultiplier
            );
            vertexWeightThreshold = EditorGUILayout.FloatField(
                new GUIContent("Leg vertex weight threshold for radius calculation", "Any vertex with a vertex weight smaller than this in the leg bone, will be ignored."),
                vertexWeightThreshold
            );
            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        // Disable button if skinnedMesh is null
            EditorGUI.BeginDisabledGroup(skinnedMesh == null);

        if (GUILayout.Button("Autofill collider radius (upper leg/leg, lower leg/knee selection required)"))
        {
            var leftRadius = PhysBoneColliderUtility.CalculateCapsuleRadiusFromLegInfluence(skinnedMesh, leftLeg, leftKnee, vertexWeightThreshold);
            var rightRadius = PhysBoneColliderUtility.CalculateCapsuleRadiusFromLegInfluence(skinnedMesh, rightLeg, rightKnee, vertexWeightThreshold);
            legColliderRadius = leftRadius > rightRadius ? leftRadius : rightRadius;
            tColliderRadius = legColliderRadius * 0.8f;
            xColliderRadius = legColliderRadius * 0.8f;
        }
        EditorGUI.EndDisabledGroup();
    }

    private void DrawLegBoneSelection()
    {
        GUILayout.Label("Leg bones Selector", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("NOTE:\n- 'Knee / Lower Leg' objects should be around the knee joint.\n- 'Leg / Upper Leg' objects should be around the hips.", MessageType.Info);
        EditorGUILayout.Space();

        GUILayout.Label("Left Leg", EditorStyles.label);
        leftLeg = (Transform)EditorGUILayout.ObjectField("Left Upper Leg/Leg", leftLeg, typeof(Transform), true);
        leftKnee = (Transform)EditorGUILayout.ObjectField("Left Lower Leg/Knee", leftKnee, typeof(Transform), true);

        EditorGUILayout.Space();

        GUILayout.Label("Right Leg", EditorStyles.label);
        rightLeg = (Transform)EditorGUILayout.ObjectField("Right Upper Leg/Leg", rightLeg, typeof(Transform), true);
        rightKnee = (Transform)EditorGUILayout.ObjectField("Right Lower Leg/Knee", rightKnee, typeof(Transform), true);
    }

    private void DrawSkirtBoneSelection()
    {
        EditorGUILayout.LabelField("Skirt Bone Elements", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Drag skirt bone GameObjects here. These will be used to compute the parent and configure constraints.", MessageType.Info);

        // Scrollable list
        skirtBoneListScroll = EditorGUILayout.BeginScrollView(skirtBoneListScroll, GUILayout.Height(150));

        for (int i = 0; i < skirtBones.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            skirtBones[i] = (GameObject)EditorGUILayout.ObjectField($"Bone {i + 1}", skirtBones[i], typeof(GameObject), true);

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                skirtBones.RemoveAt(i);
                i--;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Slot"))
        {
            skirtBones.Add(null);
        }

        if (GUILayout.Button("Import From Selection"))
        {
            foreach (var obj in Selection.gameObjects)
            {
                if (!skirtBones.Contains(obj))
                {
                    skirtBones.Add(obj);
                }
            }
        }

        if (GUILayout.Button("Clear All"))
        {
            skirtBones.Clear();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawParentingModeSection()
    {
        EditorGUILayout.LabelField("Skirt Parenting Mode", EditorStyles.boldLabel);

        autoCreateParent = EditorGUILayout.ToggleLeft(
            new GUIContent("Auto Create Skirt Parent (WARNING: THIS WILL UNPACK YOUR PREFAB, BEST TO HANDLE IT IN BLENDER, AND SELECT MANUALLY)", "Automatically creates a new empty GameObject above skirt bones."),
            autoCreateParent
        );

        if (autoCreateParent)
        {
            verticalPositionMultiplier = EditorGUILayout.FloatField(
                new GUIContent("Parent Vertical Position Multiplier", "Multiplier used to calculate the skirt parent height from the longest distance between skirt bones."),
                verticalPositionMultiplier
            );

            EditorGUILayout.Space();
            DrawSkirtBoneSelection();
        }
        else
        {
            manualParent = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Manual Skirt Parent", "Drag a GameObject here to manually set the parent for the skirt bones."),
                manualParent,
                typeof(Transform),
                true
            );
        }
    }

    private void DrawTechniqueTogglesSection()
    {
        EditorGUILayout.LabelField("Techniques", EditorStyles.boldLabel);

        enableXBoneConfig = EditorGUILayout.ToggleLeft(
            new GUIContent("Auto Configure X Bone PhysColliders (2 extra colliders)", "Automatically adds colliders in an X pattern between legs and knees."),
            enableXBoneConfig
        );
        if (enableXBoneConfig)
        {
            xColliderRadius = EditorGUILayout.FloatField(
                new GUIContent("X Bone Colliders Radius", "Capsule collider radius for X colliders. Try to keep it just below the leg's collider radius"),
                xColliderRadius
            );
        }
        EditorGUILayout.Space();


        enableTBoneConfig = EditorGUILayout.ToggleLeft(
            new GUIContent("Auto Configure T Bone PhysColliders (2 extra colliders)", "Automatically adds colliders in a T pattern between knees."),
            enableTBoneConfig
        );
        if (enableTBoneConfig)
        {
            tColliderRadius = EditorGUILayout.FloatField(
                new GUIContent("T Bone Colliders Radius", "Capsule collider radius for T colliders. Try to keep it just below the leg's collider radius"),
                tColliderRadius
            );
            tColliderLengthMultiplier = EditorGUILayout.FloatField(
                new GUIContent("T Bone Length Multiplier", "Capsule collider length multiplier for T colliders. Should be large enough to not leave any gaps when legs are spread, and small enough to avoid large it going over the knee when legs are closed "),
                tColliderLengthMultiplier
            );
        }
        EditorGUILayout.Space();


        enableLegBoneConfig = EditorGUILayout.ToggleLeft(
            new GUIContent("Auto Configure Leg Bone PhysColliders (2 extra colliders)", "Automatically adds colliders from legs to knees."),
            enableLegBoneConfig
        );
        if (enableLegBoneConfig)
        {
            legColliderRadius = EditorGUILayout.FloatField(
                new GUIContent("Leg Bone Colliders Radius", "Capsule collider radius for Leg colliders."),
                legColliderRadius
            );
        }
        EditorGUILayout.Space();


        enableRotationConstraint = EditorGUILayout.ToggleLeft(
            new GUIContent("Auto Configure Rotation Constraint on Parent (Recommended: 0.3 - 0.5)", "Adds and configures a RotationConstraint on the skirt parent so it follows the leg's movement."),
            enableRotationConstraint
        );

        if (enableRotationConstraint)
        {
            rotationConstraintWeight = EditorGUILayout.Slider(
                new GUIContent("Constraint Weight", "How strongly the rotation constraint affects the skirt parent. 0.0 to 1.0."),
                rotationConstraintWeight,
                0f,
                1f
            );
        }

    }

    private void DrawBoneCreationSection()
    {
        EditorGUILayout.LabelField("Bone Creation", EditorStyles.boldLabel);

        replaceCreatePhysBone = EditorGUILayout.ToggleLeft(
            new GUIContent("Replace/Create PhysBone in skirt", "Automatically creates a new empty GameObject above skirt bones."),
            replaceCreatePhysBone
        );

        autoReplaceColliders = EditorGUILayout.ToggleLeft(
            new GUIContent("Automatically replace old PhysBones", "Automatically replaces old colliders in new/pre-existing phys bones."),
            autoReplaceColliders
        );

        if (autoReplaceColliders)
        {
            collisionRadiusMultiplier = EditorGUILayout.FloatField(
                new GUIContent("PhysBone Radius Multiplier", "Collision radius will be computed from skirt bone tips and multiplied by this."),
                collisionRadiusMultiplier
            );
        }

    }


    private void ApplyConfiguration()
    {

        // Placeholder logic – we’ll fill in with real functionality later
        Debug.Log("Applying Autoskirt Configuration...");
        Debug.Log($"Auto Create Parent: {autoCreateParent}");
        Debug.Log($"Parent Offset: {verticalPositionMultiplier}");
        Debug.Log($"Manual Parent: {(manualParent ? manualParent.name : "None")}");

        Debug.Log($"Enable X-Bone Config: {enableXBoneConfig}");
        Debug.Log($"Enable T-Bone Config: {enableTBoneConfig}");
        Debug.Log($"Enable Rotation Constraint: {enableRotationConstraint}");
        Debug.Log($"Rotation Constraint Weight: {rotationConstraintWeight}");




        if (avatarRoot == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign an avatar root before applying configuration.", "OK");
            return;
        }

        Transform skirtParentTransform = null;

        if (autoCreateParent)
        {
            List<GameObject> validSkirtBones = skirtBones.FindAll(bone => bone != null);

            if (validSkirtBones.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Please add at least one skirt bone.", "OK");
                return;
            }

            GameObject newParent = CreateAutoSkirtParent();
            if (newParent == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to create skirt parent.", "OK");
                return;
            }

            skirtParentTransform = newParent.transform;
        }
        else
        {
            if (manualParent == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a manual skirt parent or enable auto-create.", "OK");
                return;
            }
            skirtParentTransform = manualParent;
        }


        if (enableRotationConstraint)
        {
            ConfigureRotationConstraint(skirtParentTransform, leftLeg, rightLeg);
        }

        var colliderList = new List<VRCPhysBoneCollider>();

        if (enableXBoneConfig)
        {
            var xColls = ConfigurePhysBoneCollidersX(leftLeg, rightLeg, leftKnee, rightKnee);
            if (xColls != null)
                colliderList.AddRange(xColls);
        }

        if (enableTBoneConfig)
        {
            var tColls = ConfigurePhysBoneCollidersT(leftKnee, rightKnee);
            if (tColls != null)
                colliderList.AddRange(tColls);
        }

        if (enableLegBoneConfig)
        {
            var lColls = ConfigurePhysBoneCollidersLeg(leftLeg, rightLeg, leftKnee, rightKnee);
            if (lColls != null)
                colliderList.AddRange(lColls);
        }

        // Log the result
        if (colliderList.Count > 0)
        {
            Debug.Log($"[AutoSkirt] Created {colliderList.Count} colliders:");
            foreach (var col in colliderList)
            {
                if (col != null)
                    Debug.Log($"    Collider: {col.name} at {col.transform.position}");
                else
                    Debug.Log("    Collider: (null reference)");
            }
        }
        else
        {
            Debug.LogWarning("[AutoSkirt] No colliders were created.");
        }

        if (replaceCreatePhysBone)
        {
            ConfigurePhysBones(skirtParentTransform);
        }
        if (autoReplaceColliders)
        {
            ReplacePhysBonesColliders(skirtParentTransform, colliderList);
        }

    }

    private void ConfigureRotationConstraint(Transform parent, Transform leftLeg, Transform rightLeg)
    {

        if (leftLeg == null || rightLeg == null)
        {
            Debug.LogWarning("[AutoSkirt] Legs not defined; skipping rotation constraint.");
            return;
        }

        var skirtParent = parent.gameObject;

        // Add VRC Parent Constraint
        var parentConstraint = skirtParent.AddComponent<VRCParentConstraint>();

        // Create and assign VRC constraint source array
        var newSources = new VRC.Dynamics.VRCConstraintSourceKeyableList();
        newSources.Add(new VRC.Dynamics.VRCConstraintSource
        {
            SourceTransform = leftLeg,
            Weight = 0.5f
        });
        newSources.Add(new VRC.Dynamics.VRCConstraintSource
        {
            SourceTransform = rightLeg,
            Weight = 0.5f
        });
        parentConstraint.Sources = newSources;

        // Configure the aim constraint
        parentConstraint.GlobalWeight = rotationConstraintWeight;

        parentConstraint.RotationAtRest = parent.eulerAngles;

        parentConstraint.AffectsRotationX = true;
        parentConstraint.AffectsRotationY = true;
        parentConstraint.AffectsRotationZ = true;

        parentConstraint.Locked = false;
        parentConstraint.IsActive = true;

    }

    private List<VRCPhysBoneCollider> ConfigurePhysBoneCollidersT(Transform leftKnee, Transform rightKnee)
    {
        var colliders = new List<VRCPhysBoneCollider>();

        if (leftKnee == null || rightKnee == null)
        {
            Debug.LogWarning("[AutoSkirt] Knees not defined; skipping T colliders.");
            return colliders;
        }

        // Compute midpoint between knees
        //Vector3 mid = (leftKnee.position + rightKnee.position) * 0.5f;
        float radius = tColliderRadius;
        float kneeDist = Vector3.Distance(leftKnee.position, rightKnee.position);
        float size = kneeDist * tColliderLengthMultiplier + (2 * radius);

        // Create two colliders (you might choose offsets or slightly different positions,
        // but for simplicity we place them at same midpoint)
        for (int i = 0; i < 2; i++)
        {
            Transform from = (i == 1) ? rightKnee : leftKnee;
            Transform target = (i == 0) ? rightKnee : leftKnee;

            GameObject colGO = new GameObject($"SkirtT_Collider_{i}");
            colGO.transform.SetParent(from, true);
            colGO.transform.position = from.position;

            Vector3 dir = (target.position - from.position).normalized;
            if (dir.sqrMagnitude > 0.0001f)
            {
                colGO.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
            else
            {
                colGO.transform.rotation = Quaternion.identity;
            }

            // Add PhysBone collider
            var physCol = PhysBoneColliderUtility.AddCapsuleCollider(colGO, radius, size);

            // Add VRC Aim Constraint
            // Create and assign VRC constraint source array
            var newSources = new VRC.Dynamics.VRCConstraintSourceKeyableList();
            newSources.Add(new VRC.Dynamics.VRCConstraintSource
            {
                SourceTransform = target,
                Weight = 1f
            });
            var aim = AimComponentUtility.AddAimConstraint(colGO, newSources);


            colliders.Add(physCol);
        }

        return colliders;
    }


    private List<VRCPhysBoneCollider> ConfigurePhysBoneCollidersLeg(
        Transform leftLeg, Transform rightLeg,
        Transform leftKnee, Transform rightKnee)
    {
        var colliders = new List<VRCPhysBoneCollider>();

        if (leftKnee == null || rightKnee == null || leftLeg == null || rightLeg == null)
        {
            Debug.LogWarning("[AutoSkirt] Legs or knees not defined; skipping leg colliders.");
            return colliders;
        }

        // Compute midpoint between knees
        //Vector3 mid = (leftKnee.position + rightKnee.position) * 0.5f;
        float radius = legColliderRadius;

        // Create two colliders (you might choose offsets or slightly different positions,
        // but for simplicity we place them at same midpoint)
        for (int i = 0; i < 2; i++)
        {
            Transform from = (i == 0) ? rightLeg : leftLeg;
            Transform target = (i == 0) ? rightKnee : leftKnee;
            float legKneeDist = Vector3.Distance(from.position, target.position);
            float size = legKneeDist + (2 * radius);

            GameObject colGO = new GameObject($"SkirtLeg_Collider_{i}");
            colGO.transform.SetParent(from, true);
            colGO.transform.position = from.position;

            Vector3 dir = (target.position - from.position).normalized;
            if (dir.sqrMagnitude > 0.0001f)
            {
                colGO.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
            else
            {
                colGO.transform.rotation = Quaternion.identity;
            }

            colGO.transform.Rotate(90.0f, 0.0f, 0.0f, Space.Self);

            // Add PhysBone collider
            var physCol = PhysBoneColliderUtility.AddCapsuleCollider(colGO, radius, size);

            colliders.Add(physCol);
        }

        return colliders;
    }

    private List<VRCPhysBoneCollider> ConfigurePhysBoneCollidersX(
        Transform leftLeg, Transform rightLeg,
        Transform leftKnee, Transform rightKnee)
    {
        var colliders = new List<VRCPhysBoneCollider>();

        // Validate bones
        if (leftLeg == null || rightLeg == null || leftKnee == null || rightKnee == null)
        {
            Debug.LogWarning("[AutoSkirt] Missing one or more leg/knee transforms, skipping X colliders.");
            return colliders;
        }

        // Helper local function to create one collider between two bones and Aim at target
        VRCPhysBoneCollider CreateLinkCollider(Transform from, Transform to, string name)
        {
            GameObject colGO = new GameObject(name);
            colGO.transform.SetParent(from, worldPositionStays: true);

            // Position it at midpoint
            //Vector3 mid = (from.position + to.position) * 0.5f;
            //colGO.transform.position = mid;
            colGO.transform.position = from.position;

            // Orient it so its local forward (Z) goes toward 'to'
            Vector3 dir = (to.position - from.position).normalized;
            if (dir.sqrMagnitude > 0.0001f)
            {
                colGO.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
            else
            {
                colGO.transform.rotation = Quaternion.identity;
            }


            float radius = xColliderRadius;
            // Compute height
            float height = Vector3.Distance(from.position, to.position) + (radius * 2);

            // Offset the position backward along Z by half the height
            //colGO.transform.position += colGO.transform.forward * (height * 0.5f - radius);

            // Add PhysBone collider
            var physCol = PhysBoneColliderUtility.AddCapsuleCollider(colGO, radius, height);

            // Add VRC Aim Constraint
            // Create and assign VRC constraint source array
            var newSources = new VRC.Dynamics.VRCConstraintSourceKeyableList();
            newSources.Add(new VRC.Dynamics.VRCConstraintSource
            {
                SourceTransform = to,
                Weight = 1f
            });
            var aim = AimComponentUtility.AddAimConstraint(colGO, newSources);

            return physCol;
        }

        // Create two X pattern colliders
        VRCPhysBoneCollider a = CreateLinkCollider(leftLeg, rightKnee, "SkirtX_Collider_LL_to_RK");
        colliders.Add(a);
        VRCPhysBoneCollider b = CreateLinkCollider(rightLeg, leftKnee, "SkirtX_Collider_RL_to_LK");
        colliders.Add(b);

        return colliders;
    }

    private void ConfigurePhysBones(Transform parent)
    {
        if (parent == null)
        {
            Debug.LogWarning("[Autoskirt] No skirt parent provided to ConfigurePhysBones.");
            return;
        }

        // Check if a VRCPhysBone already exists
        VRCPhysBone existingPhysBone = parent.GetComponent<VRCPhysBone>();

        if (existingPhysBone != null)
        {
            Debug.Log($"[Autoskirt] VRCPhysBone already exists on {parent.name}, deleting.");
            Undo.DestroyObjectImmediate(existingPhysBone);
        }

        // Add a new PhysBone and register for Undo
        VRCPhysBone newPhysBone = Undo.AddComponent<VRCPhysBone>(parent.gameObject);
        Debug.Log($"[Autoskirt] Added VRCPhysBone to {parent.name}.");

        // Calculate and set collision radius
        List<GameObject> validBones = new List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform childT = parent.GetChild(i);
            if (childT != null && childT.gameObject != null)
            {
                validBones.Add(childT.gameObject);
            }
        }
        List<Transform> tips = ArmatureUtility.GetBottomChildren(validBones);

        if (tips.Count >= 2)
        {
            float minDist = float.MaxValue;

            for (int i = 0; i < tips.Count; i++)
            {
                for (int j = i + 1; j < tips.Count; j++)
                {
                    float d = Vector3.Distance(tips[i].position, tips[j].position);
                    if (d < minDist)
                        minDist = d;
                }
            }

            float computedRadius = (minDist / 2f) * collisionRadiusMultiplier;
            newPhysBone.radius = computedRadius;
            newPhysBone.radiusCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            Debug.Log($"[Autoskirt] Set PhysBone radius to {computedRadius:F4} based on skirt bone tips.");
        }
        else
        {
            Debug.LogWarning("[Autoskirt] Not enough skirt bone tip points to compute radius.");
        }

        // Set limit type
        newPhysBone.limitType = VRC.Dynamics.VRCPhysBoneBase.LimitType.Angle;

        // set limit to 60
        newPhysBone.maxAngleX = 60f;
        newPhysBone.maxAngleXCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    }

    private void ReplacePhysBonesColliders(Transform parent, List<VRCPhysBoneCollider> colliderList)
    {
        if (parent == null)
        {
            Debug.LogWarning("[Autoskirt] No skirt parent provided to ConfigurePhysBones.");
            return;
        }

        // Check if a VRCPhysBone already exists
        VRCPhysBone existingPhysBone = parent.GetComponent<VRCPhysBone>();

        if (existingPhysBone == null)
        {
            Debug.Log($"[Autoskirt] VRCPhysBone doesn't exists on {parent.name}, can't add colliders.");
            return;
        }

        // Add colliders
        existingPhysBone.colliders = colliderList.OfType<VRC.Dynamics.VRCPhysBoneColliderBase>().ToList();


    }



    private GameObject CreateAutoSkirtParent()
    {
        var selectedObjects = skirtBones.FindAll(obj => obj != null);

        if (selectedObjects.Count < 2)
        {
            EditorUtility.DisplayDialog("Selection Error", "Please add two or more skirt objects to calculate the auto parent position.", "OK");
            return null;
        }

        // Step 1: Find longest distance between any two skirt bones
        float maxDistance = 0f;
        for (int i = 0; i < selectedObjects.Count; i++)
        {
            for (int j = i + 1; j < selectedObjects.Count; j++)
            {
                float distance = Vector3.Distance(selectedObjects[i].transform.position, selectedObjects[j].transform.position);
                if (distance > maxDistance)
                    maxDistance = distance;
            }
        }

        // Step 2: Calculate center point
        Vector3 center = Vector3.zero;
        foreach (var obj in selectedObjects)
        {
            center += obj.transform.position;
        }
        center /= selectedObjects.Count;

        // Step 3: Apply vertical offset
        float verticalOffset = maxDistance * verticalPositionMultiplier;
        Vector3 parentPosition = new Vector3(center.x, center.y + verticalOffset, center.z);

        // Step 4: Create new parent object
        GameObject skirtParent = new GameObject("SkirtParent_Auto");
        Undo.RegisterCreatedObjectUndo(skirtParent, "Create Skirt Parent");
        skirtParent.transform.position = parentPosition;

        // Unpack
        if (PrefabUtility.IsPartOfPrefabInstance(avatarRoot))
        {
            PrefabUtility.UnpackPrefabInstance(avatarRoot, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            Debug.Log("[Autoskirt] Unpacked prefab instance of avatar root to allow hierarchy modification.");
        }

        skirtParent.transform.SetParent(avatarRoot.transform, true);

        // Step 5: Parent all skirt bones to the new parent
        foreach (var obj in selectedObjects)
        {
            Undo.SetTransformParent(obj.transform, skirtParent.transform, "Parent Skirt Bone");
        }

        // Step 6: Make parent object child of hips bone
        Transform hipBone = avatarRoot.transform.Find("Armature/Hips");

        if (hipBone == null)
            hipBone = avatarRoot.transform.Find("Hips");

        if (hipBone == null)
            hipBone = avatarRoot.GetComponentsInChildren<Transform>()
                .FirstOrDefault(t => t.name.ToLower().Contains("hip"));

        if (hipBone != null)
        {
            skirtParent.transform.SetParent(hipBone, true);
            Debug.Log($"[Autoskirt] Skirt parent assigned to hip bone: {hipBone.name}");
        }
        else
        {
            skirtParent.transform.SetParent(avatarRoot.transform, true);
            Debug.LogWarning("[Autoskirt] Could not find hip bone, defaulted to avatar root.");
        }

        Debug.Log($"[Autoskirt] Created skirt parent at {parentPosition} with {selectedObjects.Count} skirt bones.");
        return skirtParent;
    }

}
