#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;

public static class CreateCompleteLocoController
{
    [MenuItem("Tools/Create Complete Loco Controller (P2)")]
    public static void Create()
    {
        // 1. 确保目录存在
        string dir = "Assets/Art/AnimatorControllers";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            Directory.CreateDirectory(dir);          // 物理创建
            AssetDatabase.Refresh();                 // 让 Unity 认这个文件夹
        }

        string path = dir + "/PlayerLoco_P2.controller";

        // 2. 已存在就删掉重建（避免半成品）
        if (File.Exists(path))
            AssetDatabase.DeleteAsset(path);

        // 3. 创建 Controller
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // ========== 参数（只留移动相关） ==========
        controller.AddParameter("Movement", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsBoosting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsJumping", AnimatorControllerParameterType.Bool);

        var root = controller.layers[0].stateMachine;

        // ========== 状态 ==========
        var locoState = root.AddState("Locomotion", new Vector3(300, 0, 0));
        locoState.motion = CreateLocomotionBlendTree(controller);
        root.defaultState = locoState;

        var jumpState = root.AddState("Jump", new Vector3(300, 120, 0));

        // 2. BoostFlight
        var boostState = root.AddState("BoostFlight", new Vector3(300, 240, 0));

        // 3. Attack（主动攻击，代码强制播放）
        var attackState = root.AddState("Attack", new Vector3(550, 100, 0));

        // 4. Hit（受击）
        var hitState = root.AddState("Hit", new Vector3(550, 200, 0));

        // 5. Death
        var deathState = root.AddState("Death", new Vector3(550, 300, 0));

        // ========== 过渡 ==========
        // Locomotion → Jump
        var t1 = locoState.AddTransition(jumpState);
        t1.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");
        t1.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        t1.hasExitTime = false;
        t1.duration = 0.05f;
        t1.canTransitionToSelf = false;

        // Jump → Locomotion
        var t2 = jumpState.AddTransition(locoState);
        t2.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        t2.hasExitTime = false;
        t2.duration = 0.08f;

        // Locomotion → BoostFlight
        var t3 = locoState.AddTransition(boostState);
        t3.AddCondition(AnimatorConditionMode.If, 0, "IsBoosting");
        t3.hasExitTime = false;
        t3.duration = 0.1f;

        // BoostFlight → Locomotion
        var t4 = boostState.AddTransition(locoState);
        t4.AddCondition(AnimatorConditionMode.IfNot, 0, "IsBoosting");
        t4.hasExitTime = false;
        t4.duration = 0.1f;

        // Jump → BoostFlight（可选）
        var t5 = jumpState.AddTransition(boostState);
        t5.AddCondition(AnimatorConditionMode.If, 0, "IsBoosting");
        t5.hasExitTime = false;
        t5.duration = 0.08f;

        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = controller;

        Debug.Log($"[CreateLocoController] 已生成：{path}");
    }

    static BlendTree CreateLocomotionBlendTree(AnimatorController controller)
    {
        var tree = new BlendTree
        {
            name = "LocomotionBlend",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Movement",
            useAutomaticThresholds = true
        };

        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }
}
#endif