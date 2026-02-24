using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace Thelos.AI.Editor
{
    public class AIAnimatorSetup : EditorWindow
    {
        private AnimationClip idleClip;
        private AnimationClip[] walkClips = new AnimationClip[8];
        private AnimationClip[] runClips = new AnimationClip[8];
        private AnimationClip eatClip;
        private string savePath = "Assets/Animations";
        private string controllerName = "AICharacterController";
        
        private bool useRunAnimations = true;
        private float walkSpeed = 1.5f;
        private float runSpeed = 3.5f;
        
        private Vector2 scrollPosition;
        
        [MenuItem("Tools/AI/Create AI Animator Controller")]
        private static void ShowWindow()
        {
            GetWindow<AIAnimatorSetup>("AI Animator Setup");
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("AI Animator Controller Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            GUILayout.Label("Animation Clips", EditorStyles.boldLabel);
            idleClip = EditorGUILayout.ObjectField("Idle Clip", idleClip, typeof(AnimationClip), false) as AnimationClip;
            
            EditorGUILayout.Space();
            GUILayout.Label("8-Way Walk Clips (Optional)", EditorStyles.boldLabel);
            walkClips[0] = EditorGUILayout.ObjectField("Walk Forward", walkClips[0], typeof(AnimationClip), false) as AnimationClip;
            walkClips[1] = EditorGUILayout.ObjectField("Walk Forward-Right", walkClips[1], typeof(AnimationClip), false) as AnimationClip;
            walkClips[2] = EditorGUILayout.ObjectField("Walk Right", walkClips[2], typeof(AnimationClip), false) as AnimationClip;
            walkClips[3] = EditorGUILayout.ObjectField("Walk Back-Right", walkClips[3], typeof(AnimationClip), false) as AnimationClip;
            walkClips[4] = EditorGUILayout.ObjectField("Walk Backward", walkClips[4], typeof(AnimationClip), false) as AnimationClip;
            walkClips[5] = EditorGUILayout.ObjectField("Walk Back-Left", walkClips[5], typeof(AnimationClip), false) as AnimationClip;
            walkClips[6] = EditorGUILayout.ObjectField("Walk Left", walkClips[6], typeof(AnimationClip), false) as AnimationClip;
            walkClips[7] = EditorGUILayout.ObjectField("Walk Forward-Left", walkClips[7], typeof(AnimationClip), false) as AnimationClip;
            
            EditorGUILayout.Space();
            useRunAnimations = EditorGUILayout.Toggle("Use Run Animations", useRunAnimations);
            
            if (useRunAnimations)
            {
                EditorGUILayout.Space();
                GUILayout.Label("8-Way Run Clips (Optional)", EditorStyles.boldLabel);
                runClips[0] = EditorGUILayout.ObjectField("Run Forward", runClips[0], typeof(AnimationClip), false) as AnimationClip;
                runClips[1] = EditorGUILayout.ObjectField("Run Forward-Right", runClips[1], typeof(AnimationClip), false) as AnimationClip;
                runClips[2] = EditorGUILayout.ObjectField("Run Right", runClips[2], typeof(AnimationClip), false) as AnimationClip;
                runClips[3] = EditorGUILayout.ObjectField("Run Back-Right", runClips[3], typeof(AnimationClip), false) as AnimationClip;
                runClips[4] = EditorGUILayout.ObjectField("Run Backward", runClips[4], typeof(AnimationClip), false) as AnimationClip;
                runClips[5] = EditorGUILayout.ObjectField("Run Back-Left", runClips[5], typeof(AnimationClip), false) as AnimationClip;
                runClips[6] = EditorGUILayout.ObjectField("Run Left", runClips[6], typeof(AnimationClip), false) as AnimationClip;
                runClips[7] = EditorGUILayout.ObjectField("Run Forward-Left", runClips[7], typeof(AnimationClip), false) as AnimationClip;
                
                EditorGUILayout.Space();
                GUILayout.Label("Speed Thresholds", EditorStyles.boldLabel);
                walkSpeed = EditorGUILayout.FloatField("Walk Speed Threshold", walkSpeed);
                runSpeed = EditorGUILayout.FloatField("Run Speed Threshold", runSpeed);
                EditorGUILayout.HelpBox("Speed parameter will blend between walk and run animations", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            eatClip = EditorGUILayout.ObjectField("Eat Clip", eatClip, typeof(AnimationClip), false) as AnimationClip;
            
            EditorGUILayout.Space();
            GUILayout.Label("Output Settings", EditorStyles.boldLabel);
            savePath = EditorGUILayout.TextField("Save Path", savePath);
            controllerName = EditorGUILayout.TextField("Controller Name", controllerName);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Create Animator Controller", GUILayout.Height(30)))
            {
                CreateAnimatorController();
            }
            
            EditorGUILayout.Space();
            
            string helpText = "This will create an Animator Controller with:\n" +
                "- Idle state\n";
            
            if (useRunAnimations)
            {
                helpText += "- Nested blend tree: Speed controls Walk/Run, MoveX/MoveY control direction\n";
                helpText += "- Walk animations at radius 1.5, Run at radius 1.5\n";
                helpText += "- Speed thresholds: Walk=" + walkSpeed + ", Run=" + runSpeed + "\n";
                helpText += "- Parameters: MoveX, MoveY, Speed, IsMoving, Eat, IsEating";
            }
            else
            {
                helpText += "- Simple 2D blend tree (walk only)\n";
                helpText += "- Parameters: MoveX, MoveY, IsMoving, Eat, IsEating";
            }
            
            EditorGUILayout.HelpBox(helpText, MessageType.Info);
            
            EditorGUILayout.EndScrollView();
        }
        
        private void CreateAnimatorController()
        {
            if (!System.IO.Directory.Exists(savePath))
            {
                System.IO.Directory.CreateDirectory(savePath);
            }
            
            string path = $"{savePath}/{controllerName}.controller";
            
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            
            // Add parameters
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Eat", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsEating", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            
            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
            
            // Create Idle state
            AnimatorState idleState = rootStateMachine.AddState("Idle");
            if (idleClip != null)
            {
                idleState.motion = idleClip;
            }
            
            // Create Movement state with single unified blend tree
            AnimatorState moveState = rootStateMachine.AddState("Move");
            BlendTree moveBlendTree = CreateUnifiedMovementBlendTree(controller);
            moveState.motion = moveBlendTree;
            
            // Create Eat state
            AnimatorState eatState = rootStateMachine.AddState("Eat");
            if (eatClip != null)
            {
                eatState.motion = eatClip;
            }
            
            rootStateMachine.defaultState = idleState;
            
            // Transitions: Idle <-> Move
            AnimatorStateTransition idleToMove = idleState.AddTransition(moveState);
            idleToMove.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            idleToMove.hasExitTime = false;
            idleToMove.duration = 0.1f;
            
            AnimatorStateTransition moveToIdle = moveState.AddTransition(idleState);
            moveToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            moveToIdle.hasExitTime = false;
            moveToIdle.duration = 0.1f;
            
            // Transitions: Any State -> Eat
            AnimatorStateTransition anyToEat = rootStateMachine.AddAnyStateTransition(eatState);
            anyToEat.AddCondition(AnimatorConditionMode.If, 0, "Eat");
            anyToEat.hasExitTime = false;
            anyToEat.duration = 0.1f;
            
            // Transitions: Eat -> Idle
            AnimatorStateTransition eatToIdle = eatState.AddTransition(idleState);
            eatToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsEating");
            eatToIdle.hasExitTime = false;
            eatToIdle.duration = 0.3f;
            
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", $"Animator Controller created at: {path}", "OK");
            Selection.activeObject = controller;
        }
        
        private BlendTree CreateUnifiedMovementBlendTree(AnimatorController controller)
        {
            if (useRunAnimations)
            {
                return CreateNestedSpeedBlendTree(controller);
            }
            else
            {
                return CreateSimple2DBlendTree(controller);
            }
        }
        
        private BlendTree CreateNestedSpeedBlendTree(AnimatorController controller)
        {
            BlendTree rootBlendTree = new BlendTree
            {
                name = "Movement",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            
            BlendTree walkBlendTree = new BlendTree
            {
                name = "Walk",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY"
            };
            
            BlendTree runBlendTree = new BlendTree
            {
                name = "Run",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY"
            };
            
            Vector2[] directions = new Vector2[]
            {
                new Vector2(0, 1),
                new Vector2(0.707f, 0.707f),
                new Vector2(1, 0),
                new Vector2(0.707f, -0.707f),
                new Vector2(0, -1),
                new Vector2(-0.707f, -0.707f),
                new Vector2(-1, 0),
                new Vector2(-0.707f, 0.707f)
            };
            
            float walkRadius = 1.5f;
            for (int i = 0; i < walkClips.Length; i++)
            {
                if (walkClips[i] != null)
                {
                    Vector2 walkPos = directions[i] * walkRadius;
                    walkBlendTree.AddChild(walkClips[i], walkPos);
                }
            }
            
            if (idleClip != null)
            {
                walkBlendTree.AddChild(idleClip, Vector2.zero);
            }
            
            float runRadius = 1.5f;
            for (int i = 0; i < runClips.Length; i++)
            {
                if (runClips[i] != null)
                {
                    Vector2 runPos = directions[i] * runRadius;
                    runBlendTree.AddChild(runClips[i], runPos);
                }
            }
            
            AssetDatabase.AddObjectToAsset(walkBlendTree, controller);
            AssetDatabase.AddObjectToAsset(runBlendTree, controller);
            
            rootBlendTree.AddChild(walkBlendTree, walkSpeed);
            rootBlendTree.AddChild(runBlendTree, runSpeed);
            
            AssetDatabase.AddObjectToAsset(rootBlendTree, controller);
            
            return rootBlendTree;
        }
        
        private BlendTree CreateSimple2DBlendTree(AnimatorController controller)
        {
            BlendTree blendTree = new BlendTree
            {
                name = "Movement",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY"
            };
            
            Vector2[] directions = new Vector2[]
            {
                new Vector2(0, 1),
                new Vector2(0.707f, 0.707f),
                new Vector2(1, 0),
                new Vector2(0.707f, -0.707f),
                new Vector2(0, -1),
                new Vector2(-0.707f, -0.707f),
                new Vector2(-1, 0),
                new Vector2(-0.707f, 0.707f)
            };
            
            float walkRadius = 1.5f;
            for (int i = 0; i < walkClips.Length; i++)
            {
                if (walkClips[i] != null)
                {
                    Vector2 walkPos = directions[i] * walkRadius;
                    blendTree.AddChild(walkClips[i], walkPos);
                }
            }
            
            if (idleClip != null)
            {
                blendTree.AddChild(idleClip, Vector2.zero);
            }
            
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            
            return blendTree;
        }
    }
}
