#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CreateCompleteLocoController
{
    [MenuItem("Tools/Create Complete Loco Controller (P2)")]
    public static void Create()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Complete Loco Controller",
            "PlayerLoco_P2",
            "controller",
            "选择保存位置");

        if (string.IsNullOrEmpty(path))
            return;

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // ========== 参数 ==========
        controller.AddParameter("Movement", AnimatorControllerParameterType.Float);   // 0=Idle, 1=Walk, 2=Run
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsBoosting", AnimatorControllerParameterType.Bool);

        // ========== Base Layer ==========
        var root = controller.layers[0].stateMachine;

        // 1. Locomotion (Blend Tree)
        var locoState = root.AddState("Locomotion", new Vector3(300, 100, 0));
        var blendTree = new BlendTree
        {
            name = "LocomotionBlend",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Movement",
            useAutomaticThresholds = false
        };
        blendTree.AddChild(null, 0f); // Idle
        blendTree.AddChild(null, 1f); // Walk
        blendTree.AddChild(null, 2f); // Run
        locoState.motion = blendTree;
        AssetDatabase.AddObjectToAsset(blendTree, controller);

        // 2. BoostFlight
        var boostState = root.AddState("BoostFlight", new Vector3(300, 250, 0));

        // 3. Attack（主动攻击，代码强制播放）
        var attackState = root.AddState("Attack", new Vector3(550, 100, 0));

        // 4. Hit（受击）
        var hitState = root.AddState("Hit", new Vector3(550, 200, 0));

        // 5. Death
        var deathState = root.AddState("Death", new Vector3(550, 300, 0));

        // 默认状态
        root.defaultState = locoState;

        // ========== 只保留移动 ↔ 飞行 的 Transition ==========
        var toBoost = locoState.AddTransition(boostState);
        toBoost.AddCondition(AnimatorConditionMode.If, 0, "IsBoosting");
        toBoost.hasExitTime = false;
        toBoost.duration = 0.1f;

        var toLoco = boostState.AddTransition(locoState);
        toLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsBoosting");
        toLoco.hasExitTime = false;
        toLoco.duration = 0.15f;

        // Attack / Hit / Death 全部不连线，只靠代码 Play

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);

        Debug.Log("已生成完整 P2 Controller：\n" +
                  "- Locomotion (Blend Tree)\n" +
                  "- BoostFlight\n" +
                  "- Attack（代码强制播放）\n" +
                  "- Hit（受击）\n" +
                  "- Death\n" +
                  "请手动把对应 AnimationClip 拖进去。");
    }
}
#endif