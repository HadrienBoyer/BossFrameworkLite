using System.Collections.Generic;
using System.IO;
using TappyTale.BossFight.Editor.Install;
using TappyTale.BossFight.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TappyTale.BossFight.Editor.Player
{
    public static class PlayerAnimatorBuilder
    {
        public static AnimatorController BuildAnimatorController(PlayerDefinition definition, string customOutputPath = null)
        {
            if (definition == null)
            {
                Debug.LogWarning("[Boss Fight Framework] Cannot build AnimatorController for null PlayerDefinition.");
                return null;
            }

            string outputPath = customOutputPath;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                string defPath = AssetDatabase.GetAssetPath(definition);
                string dir = !string.IsNullOrEmpty(defPath)
                    ? Path.GetDirectoryName(defPath)?.Replace('\\', '/')
                    : BossFrameworkInstaller.DataFolder;
                if (string.IsNullOrEmpty(dir)) dir = BossFrameworkInstaller.DataFolder;
                BossFrameworkInstaller.EnsureFolderHierarchy(dir);
                outputPath = $"{dir}/AC_{definition.name}.controller";
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(outputPath);

            // 1. Parameters matching PlayerAnimatorAdapter
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("StrafeX", AnimatorControllerParameterType.Float);
            controller.AddParameter("StrafeY", AnimatorControllerParameterType.Float);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsLockedOn", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDashing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsStaggered", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("LightAttack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HeavyAttack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Parry", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine rootSm = controller.layers[0].stateMachine;
            rootSm.name = "BaseLocomotionAndCombat";

            // 2. Free Locomotion 1D BlendTree (Idle -> Walk -> Run -> Sprint)
            AnimatorState freeLocomotionState = controller.CreateBlendTreeInController("FreeLocomotion", out BlendTree freeTree, 0);
            freeTree.blendType = BlendTreeType.Simple1D;
            freeTree.blendParameter = "MoveSpeed";
            freeTree.useAutomaticThresholds = false;
            freeTree.AddChild(definition.IdleClip, 0f);
            freeTree.AddChild(definition.WalkClip != null ? definition.WalkClip : definition.RunClip, 0.35f);
            freeTree.AddChild(definition.RunClip, 0.75f);
            freeTree.AddChild(definition.SprintClip != null ? definition.SprintClip : definition.RunClip, 1.15f);
            rootSm.defaultState = freeLocomotionState;

            // 3. Lock-On Strafe 2D Freeform Directional BlendTree
            AnimatorState strafeLocomotionState = controller.CreateBlendTreeInController("LockOnStrafe", out BlendTree strafeTree, 0);
            strafeTree.blendType = BlendTreeType.FreeformDirectional2D;
            strafeTree.blendParameter = "StrafeX";
            strafeTree.blendParameterY = "StrafeY";
            strafeTree.AddChild(definition.IdleClip, Vector2.zero);
            strafeTree.AddChild(definition.StrafeForwardClip != null ? definition.StrafeForwardClip : definition.WalkClip, new Vector2(0f, 1f));
            strafeTree.AddChild(definition.StrafeBackClip != null ? definition.StrafeBackClip : definition.WalkClip, new Vector2(0f, -1f));
            strafeTree.AddChild(definition.StrafeLeftClip != null ? definition.StrafeLeftClip : definition.WalkClip, new Vector2(-1f, 0f));
            strafeTree.AddChild(definition.StrafeRightClip != null ? definition.StrafeRightClip : definition.WalkClip, new Vector2(1f, 0f));

            // Transitions between Free and Strafe Locomotion
            AnimatorStateTransition toStrafe = freeLocomotionState.AddTransition(strafeLocomotionState);
            toStrafe.hasExitTime = false;
            toStrafe.duration = 0.15f;
            toStrafe.AddCondition(AnimatorConditionMode.If, 0f, "IsLockedOn");

            AnimatorStateTransition toFree = strafeLocomotionState.AddTransition(freeLocomotionState);
            toFree.hasExitTime = false;
            toFree.duration = 0.15f;
            toFree.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsLockedOn");

            // 4. Action States (Jump, Fall, Dash, LightAttack, HeavyAttack, Parry, Stagger, Death)
            AnimatorState jumpState = CreateActionState(rootSm, "Jump", definition.JumpClip, freeLocomotionState, true, 0.85f);
            AnimatorState fallState = rootSm.AddState("Fall");
            fallState.motion = definition.JumpClip;
            AnimatorStateTransition fallToGround = fallState.AddTransition(freeLocomotionState);
            fallToGround.hasExitTime = false;
            fallToGround.duration = 0.12f;
            fallToGround.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");

            AnimatorStateTransition jumpToFall = jumpState.AddTransition(fallState);
            jumpToFall.hasExitTime = true;
            jumpToFall.exitTime = 0.7f;
            jumpToFall.duration = 0.15f;
            jumpToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");

            AnimatorState dashState = CreateActionState(rootSm, "Dash", definition.DashClip, freeLocomotionState, true, 0.9f);
            AnimatorState lightAttackState = CreateActionState(rootSm, "LightAttack", definition.LightAttackClip, freeLocomotionState, true, 0.9f);
            AnimatorState heavyAttackState = CreateActionState(rootSm, "HeavyAttack", definition.HeavyAttackClip, freeLocomotionState, true, 0.92f);
            AnimatorState parryState = CreateActionState(rootSm, "Parry", definition.ParryClip, freeLocomotionState, true, 0.88f);

            AnimatorState staggerState = rootSm.AddState("Stagger");
            staggerState.motion = definition.StaggerClip;
            AnimatorStateTransition staggerExit = staggerState.AddTransition(freeLocomotionState);
            staggerExit.hasExitTime = false;
            staggerExit.duration = 0.12f;
            staggerExit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsStaggered");

            AnimatorState deathState = rootSm.AddState("Death");
            deathState.motion = definition.DeathClip;

            // AnyState transitions
            AddAnyStateTriggerTransition(rootSm, jumpState, "Jump");
            AddAnyStateTriggerTransition(rootSm, dashState, "Dash");
            AddAnyStateTriggerTransition(rootSm, lightAttackState, "LightAttack");
            AddAnyStateTriggerTransition(rootSm, heavyAttackState, "HeavyAttack");
            AddAnyStateTriggerTransition(rootSm, parryState, "Parry");

            AnimatorStateTransition anyToStagger = rootSm.AddAnyStateTransition(staggerState);
            anyToStagger.hasExitTime = false;
            anyToStagger.duration = 0.08f;
            anyToStagger.canTransitionToSelf = false;
            anyToStagger.AddCondition(AnimatorConditionMode.If, 0f, "IsStaggered");

            AnimatorStateTransition anyToDeath = rootSm.AddAnyStateTransition(deathState);
            anyToDeath.hasExitTime = false;
            anyToDeath.duration = 0.1f;
            anyToDeath.canTransitionToSelf = false;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");

            // Bind generated controller back to PlayerDefinition
            BossFrameworkInstaller.Configure(definition, "animatorController", controller);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Boss Fight Framework] Generated AnimatorController '{outputPath}' for '{definition.DisplayName}'.");
            return controller;
        }

        public static List<string> ValidateClips(PlayerDefinition definition)
        {
            List<string> notes = new();
            if (definition == null)
            {
                notes.Add("PlayerDefinition is null.");
                return notes;
            }

            CheckClip(notes, "IdleClip", definition.IdleClip);
            CheckClip(notes, "WalkClip", definition.WalkClip);
            CheckClip(notes, "RunClip", definition.RunClip);
            CheckClip(notes, "SprintClip", definition.SprintClip);
            CheckClip(notes, "StrafeForwardClip", definition.StrafeForwardClip);
            CheckClip(notes, "StrafeBackClip", definition.StrafeBackClip);
            CheckClip(notes, "StrafeLeftClip", definition.StrafeLeftClip);
            CheckClip(notes, "StrafeRightClip", definition.StrafeRightClip);
            CheckClip(notes, "JumpClip", definition.JumpClip);
            CheckClip(notes, "DashClip", definition.DashClip);
            CheckClip(notes, "LightAttackClip", definition.LightAttackClip);
            CheckClip(notes, "HeavyAttackClip", definition.HeavyAttackClip);
            CheckClip(notes, "ParryClip", definition.ParryClip);

            if (notes.Count == 0)
            {
                notes.Add("All locomotion, strafe, and combat animation clips are assigned.");
            }
            else
            {
                notes.Insert(0, "Note: PlayerController works 100% without an Animator or clips (capsule fallback active). Optional unassigned clips:");
            }

            return notes;
        }

        private static void CheckClip(List<string> notes, string label, AnimationClip clip)
        {
            if (clip == null)
            {
                notes.Add($"- {label}: unassigned (optional)");
            }
        }

        private static AnimatorState CreateActionState(
            AnimatorStateMachine sm,
            string stateName,
            AnimationClip clip,
            AnimatorState returnState,
            bool hasExitTime,
            float exitTime)
        {
            AnimatorState state = sm.AddState(stateName);
            state.motion = clip;
            AnimatorStateTransition exit = state.AddTransition(returnState);
            exit.hasExitTime = hasExitTime;
            exit.exitTime = exitTime;
            exit.duration = 0.12f;
            return state;
        }

        private static void AddAnyStateTriggerTransition(AnimatorStateMachine sm, AnimatorState destination, string triggerParam)
        {
            AnimatorStateTransition transition = sm.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.duration = 0.08f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, triggerParam);
        }
    }
}
