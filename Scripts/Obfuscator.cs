#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using System.Linq;

using Kanna.Protecc;

using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

using UnityEngine;

using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

// A huge thank you to kamyu! This module would not be possible without them!

namespace Kanna.Protecc
{
    public class Obfuscator
    {
        private static readonly string[] SkipParameterNames =
        {
            "IsLocal",
            "Viseme",
            "GestureLeft",
            "GestureRight",
            "GestureLeftWeight",
            "GestureRightWeight",
            "AngularY",
            "VelocityX",
            "VelocityY",
            "VelocityZ",
            "Upright",
            "Grounded",
            "Seated",
            "AFK",
            "Expression1",
            "Expression2",
            "Expression3",
            "Expression4",
            "Expression5",
            "Expression6",
            "Expression7",
            "Expression8",
            "Expression9",
            "Expression10",
            "Expression11",
            "Expression12",
            "Expression13",
            "Expression14",
            "Expression15",
            "Expression16",
            "TrackingType",
            "VRMode",
            "MuteSelf",
            "InStation",
            "VRCEmote",
            "VRCFaceBlendV",
            "VRCFaceBlendH"
        };

        private readonly Dictionary<string, string> _parameterDic = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _objectNameDic = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _filePathDic = new Dictionary<string, string>();
        private readonly List<AnimationClip> _animClipList = new List<AnimationClip>();
        private readonly HashSet<string> _excludeNameSet = new HashSet<string>();
        private uint _tempIndex;

        public void Obfuscate(GameObject gobj, KannaProteccRoot root)
        {
            _parameterDic.Clear();
            _objectNameDic.Clear();
            _filePathDic.Clear();
            _animClipList.Clear();
            _excludeNameSet.Clear();
            _tempIndex = 0;
            GC.Collect();

            try
            {
                if (string.IsNullOrEmpty(root.path))
                    root.path = root.pathPrefix + DateTimeOffset.Now.ToUnixTimeMilliseconds();

                CreateFolder(root.path);

                ProgressBar("Clone Avatar Object", 1);
                var o = gobj;
                var gameObjectName = o.name + "_Obfuscated";
                var obj = Object.Instantiate(o);
                obj.name = gameObjectName;
                obj.SetActive(true);
                o.SetActive(false);

                ProgressBar("Remove animatorController of RootAnimator", 2);
                var rootAnimator = obj.GetComponent<Animator>();
                if (rootAnimator != null) rootAnimator.runtimeAnimatorController = null;

                ProgressBar("Find AvatarDescriptor Component", 3);

                var avatar = obj.GetComponent<VRCAvatarDescriptor>();

                ProgressBar("ExpressionParameters obfuscation", 4);
                if (avatar.expressionParameters != null)
                    avatar.expressionParameters = ExpressionParametersObfuscator(avatar.expressionParameters, root);

                ProgressBar("ExpressionsMenu obfuscation", 5);
                if (avatar.expressionsMenu != null)
                    avatar.expressionsMenu = ExpressionsMenuObfuscator(avatar.expressionsMenu, root);

                ProgressBar("baseAnimationLayers animatorController obfuscation", 6);
                var animationLayers = avatar.baseAnimationLayers;
                for (var i = 0; i < animationLayers.Length; ++i)
                {
                    if (animationLayers[i].animatorController == null) continue;
                    var animator = animationLayers[i].animatorController;
                    animationLayers[i].animatorController = AnimatorObfuscator((AnimatorController)animator, root);
                }

                avatar.baseAnimationLayers = animationLayers;

                ProgressBar("specialAnimationLayers animatorController obfuscation", 7);
                var specialAnimationLayers = avatar.specialAnimationLayers;
                for (var i = 0; i < specialAnimationLayers.Length; ++i)
                {
                    if (specialAnimationLayers[i].animatorController == null) continue;
                    var animator = specialAnimationLayers[i].animatorController;
                    specialAnimationLayers[i].animatorController = AnimatorObfuscator((AnimatorController)animator, root);
                }

                avatar.specialAnimationLayers = specialAnimationLayers;

                ProgressBar("Another animatorController obfuscation", 8);
                var otherAnimators = obj.GetComponentsInChildren<Animator>(true)
                    .Where(t => t.runtimeAnimatorController == null || t.gameObject != obj);
                foreach (var animator in otherAnimators)
                    animator.runtimeAnimatorController =
                        AnimatorObfuscator((AnimatorController)animator.runtimeAnimatorController, root);

                ProgressBar("Get all bones from animator", 9);
                var animators = obj.GetComponentsInChildren<Animator>(true);
                var enumValues = Enum.GetValues(typeof(HumanBodyBones));
                foreach (HumanBodyBones boneId in enumValues)
                {
                    if (boneId == HumanBodyBones.LastBone) continue;
                    foreach (var a in animators)
                    {
                        var boneTransform = a.GetBoneTransform(boneId);
                        if (boneTransform == null) continue;

                        AddAllParent(obj, a, boneTransform);
                    }
                }

                if (!root.disableObjectNameObfuscation)
                {
                    foreach (var objectName in root.excludeObjectNames.Where(item => item != null && !string.IsNullOrWhiteSpace(item.name)))
                    {
                        _excludeNameSet.Add(objectName.name);
                    }

                    ProgressBar("Object name obfuscation", 10);
                    var children = obj.GetComponentsInChildren<Transform>(true).Where(t => t != obj.transform).ToList();

                    foreach (var childObject in children.Select(child => child.gameObject)
                                 .Where(childObject => childObject.GetInstanceID() != obj.GetInstanceID() &&
                                                       !_excludeNameSet.Contains(childObject.name)))
                    {
                        if (!_objectNameDic.ContainsKey(childObject.name))
                        {
                            var newName = GUID.Generate().ToString();
                            while (_objectNameDic.ContainsKey(newName))
                                newName = GUID.Generate().ToString();

                            _objectNameDic.Add(childObject.name, newName);
                        }

                        childObject.name = _objectNameDic[childObject.name];
                    }

                    ProgressBar("Update AnimationClips", 11);
                    foreach (var clip in _animClipList)
                    {
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                        {
                            var copy = binding;
                            var bindingPath = binding.path;

                            var names = bindingPath.Split('/');
                            for (var i = 0; i < names.Length; i++)
                                names[i] = _objectNameDic.ContainsKey(names[i])
                                    ? _objectNameDic[names[i]]
                                    : names[i];

                            copy.path = string.Join("/", names);
                            var curve = AnimationUtility.GetEditorCurve(clip, binding);
                            AnimationUtility.SetEditorCurve(clip, binding, null);
                            AnimationUtility.SetEditorCurve(clip, copy, curve);
                        }

                        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                        {
                            var copy = binding;
                            var bindingPath = binding.path;

                            var names = bindingPath.Split('/');
                            for (var i = 0; i < names.Length; i++)
                                names[i] = _objectNameDic.ContainsKey(names[i])
                                    ? _objectNameDic[names[i]]
                                    : names[i];

                            copy.path = string.Join("/", names);
                            var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                            AnimationUtility.SetObjectReferenceCurve(clip, copy, curve);
                        }
                    }
                }

                ProgressBar("Done!", 1, 1);
            }
            catch (Exception err)
            {
                Debug.LogError(err);
            }
            finally
            {
                _parameterDic.Clear();
                _objectNameDic.Clear();
                _animClipList.Clear();
                _filePathDic.Clear();
                _excludeNameSet.Clear();
                _tempIndex = 0;

                EditorSceneManager.SaveOpenScenes();
                EditorUtility.ClearProgressBar();

                GC.Collect();
                AssetDatabase.ImportAsset(root.path, ImportAssetOptions.ImportRecursive);
            }
        }

        private void AddAllParent(GameObject root, Animator animator, Transform bone)
        {
            var parent = bone.parent;
            if (bone.parent != null && animator.gameObject != parent.gameObject && parent.gameObject != root)
                AddAllParent(root, animator, bone.parent);

            AddNameFilter(bone);
        }

        private void AddNameFilter(Transform bone)
        {
            var gameObjectName = bone.gameObject.name;

            if (!_excludeNameSet.Contains(gameObjectName))
                _excludeNameSet.Add(gameObjectName);
        }

#if VRC_SDK_VRCSDK3
        private VRCExpressionParameters ExpressionParametersObfuscator(VRCExpressionParameters oldEp, KannaProteccRoot root)
        {
            root.ParameterRenamedValues.Clear();

            var expressionParameters = CopyAssetFile("asset", oldEp, root);
            var parameters = expressionParameters.parameters.ToList().Where(p => !string.IsNullOrEmpty(p.name.Trim()))
                .ToList();
            foreach (var parameter in parameters.Where(parameter => Array.FindIndex(SkipParameterNames, value => value == parameter.name) == -1 && Array.FindIndex(root.excludeParamNames.ToArray(), value => value == parameter.name) == -1))
            {
                if (_parameterDic.ContainsKey(parameter.name))
                {
                    parameter.name = _parameterDic[parameter.name];
                    continue;
                }

                var newName = GUID.Generate().ToString();
                while (_parameterDic.ContainsKey(newName)) newName = GUID.Generate().ToString();

                _parameterDic.Add(parameter.name, newName);
                if (parameter.name.StartsWith("BitKey"))
                {
                    if (root.ParameterRenamedValues.FindIndex(o => o.Key == parameter.name) is var index && index != -1)
                    {
                        root.ParameterRenamedValues[index] = new KeyValuePair<string, string>(parameter.name, newName);
                    }
                    else
                    {
                        root.ParameterRenamedValues.Add(new KeyValuePair<string, string>(parameter.name, newName));
                    }
                }
                parameter.name = newName;
            }

            parameters = expressionParameters.parameters.ToList();
            while (120 - expressionParameters.CalcTotalCost() > 0)
            {
                var newName = GUID.Generate().ToString();
                while (_parameterDic.ContainsKey(newName)) newName = GUID.Generate().ToString();

                parameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = newName,
                    saved = Random.Range(0, 2) == 0,
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = (float)(Math.Truncate((double)Random.Range(0f, 1f) * 100) / 100)
                });

                expressionParameters.parameters = parameters.ToArray();
            }

            while (128 - expressionParameters.CalcTotalCost() > 0)
            {
                var newName = GUID.Generate().ToString();
                while (_parameterDic.ContainsKey(newName)) newName = GUID.Generate().ToString();

                parameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = newName,
                    saved = Random.Range(0, 2) == 0,
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = Random.Range(0, 2) == 0 ? 1 : 0
                });

                expressionParameters.parameters = parameters.ToArray();
            }

            parameters = parameters.OrderBy(a => Guid.NewGuid()).ToList();
            expressionParameters.parameters = parameters.ToArray();
            return expressionParameters;
        }

        private VRCExpressionsMenu ExpressionsMenuObfuscator(VRCExpressionsMenu menu, KannaProteccRoot root)
        {
            var expressionsMenu = CopyAssetFile("asset", menu, root);
            foreach (var control in expressionsMenu.controls)
            {
                if (control.subMenu != null) control.subMenu = ExpressionsMenuObfuscator(control.subMenu, root);
                if (control.parameter != null)
                    control.parameter.name = _parameterDic.ContainsKey(control.parameter.name)
                        ? _parameterDic[control.parameter.name]
                        : control.parameter.name;
                if (control.subParameters != null)
                    foreach (var param in control.subParameters)
                        param.name = _parameterDic.ContainsKey(param.name)
                            ? _parameterDic[param.name]
                            : param.name;
            }

            return expressionsMenu;
        }
#endif

        private AnimatorController AnimatorObfuscator(AnimatorController controller, KannaProteccRoot root)
        {
            if (controller == null) return null;
            var animator = CopyAssetFile("controller", controller, root);
            var parameters = animator.parameters.ToList();
            foreach (var t in parameters)
            {
                if (_parameterDic.ContainsKey(t.name))
                {
                    t.name = _parameterDic[t.name];
                }
                else if (Array.FindIndex(SkipParameterNames, value => value == t.name) == -1 && Array.FindIndex(root.excludeParamNames.ToArray(), value => value == t.name) == -1)
                {
                    var newName = GUID.Generate().ToString();
                    while (_parameterDic.ContainsKey(newName)) newName = GUID.Generate().ToString();

                    _parameterDic.Add(t.name, newName);
                    t.name = newName;
                }
            }

            parameters = parameters.OrderBy(a => Guid.NewGuid()).ToList();
            animator.parameters = parameters.ToArray();

            var layers = animator.layers.ToList();
            foreach (var layer in layers)
            {
                var newLayerName = GUID.Generate().ToString() + _tempIndex;
                _tempIndex++;
                layer.name = newLayerName;
                layer.stateMachine = StateMachineObfuscator(layer.name, layer.stateMachine, root);
            }

            animator.layers = layers.ToArray();

            return animator;
        }

        private ChildAnimatorStateMachine ChildStateMachineObfuscator(ChildAnimatorStateMachine stateMachine, KannaProteccRoot root)
        {
            var newName = GUID.Generate().ToString() + _tempIndex;
            _tempIndex++;
            return new ChildAnimatorStateMachine
            {
                position = Vector3.zero,
                stateMachine = StateMachineObfuscator(newName, stateMachine.stateMachine, root),
            };
            ;
        }

        private AnimatorStateMachine StateMachineObfuscator(string stateMachineName, AnimatorStateMachine stateMachine, KannaProteccRoot root)
        {
            stateMachine.name = stateMachineName;

#if VRC_SDK_VRCSDK3
            var behaviours = stateMachine.behaviours.ToList();
            foreach (var parameter in behaviours.OfType<VRCAvatarParameterDriver>()
                         .SelectMany(behaviour => behaviour.parameters))
                parameter.name = _parameterDic.ContainsKey(parameter.name)
                    ? _parameterDic[parameter.name]
                    : parameter.name;

            stateMachine.behaviours = behaviours.ToArray();
#endif

            stateMachine.anyStatePosition = Vector3.zero;
            stateMachine.exitPosition = Vector3.zero;
            stateMachine.parentStateMachinePosition = Vector3.zero;
            stateMachine.entryPosition = Vector3.zero;

            var childStates = new List<ChildAnimatorState>();
            foreach (var t in stateMachine.states)
                childStates.Add(
                    new ChildAnimatorState
                    {
                        position = Vector3.zero,
                        state = AnimatorStateObfuscator(t.state, root)
                    }
                );
            stateMachine.states = childStates.ToArray();


            var stateMachines = new List<ChildAnimatorStateMachine>();
            foreach (var t in stateMachine.stateMachines)
                stateMachines.Add(ChildStateMachineObfuscator(t, root));
            stateMachine.stateMachines = stateMachines.ToArray();


            var entryTransitions = stateMachine.entryTransitions.ToList();
            foreach (var transition in entryTransitions)
            {
                var conditions = new List<AnimatorCondition>();
                foreach (var condition in transition.conditions)
                    conditions.Add(new AnimatorCondition
                    {
                        mode = condition.mode,
                        parameter = _parameterDic.ContainsKey(condition.parameter)
                            ? _parameterDic[condition.parameter]
                            : condition.parameter,
                        threshold = condition.threshold
                    });

                transition.conditions = conditions.ToArray();
            }

            stateMachine.entryTransitions = entryTransitions.ToArray();

            var anyStateTransitions = stateMachine.anyStateTransitions.ToList();
            foreach (var transition in anyStateTransitions)
            {
                var conditions = new List<AnimatorCondition>();
                foreach (var condition in transition.conditions)
                    conditions.Add(new AnimatorCondition
                    {
                        mode = condition.mode,
                        parameter = _parameterDic.ContainsKey(condition.parameter)
                            ? _parameterDic[condition.parameter]
                            : condition.parameter,
                        threshold = condition.threshold
                    });

                transition.conditions = conditions.ToArray();
            }

            stateMachine.anyStateTransitions = anyStateTransitions.ToArray();

            return stateMachine;
        }

        private AnimatorState AnimatorStateObfuscator(AnimatorState state, KannaProteccRoot root)
        {
            state.name = GUID.Generate() + "_" + _tempIndex;
            _tempIndex++;

#if VRC_SDK_VRCSDK3
            var behaviours = state.behaviours.ToList();
            foreach (var parameter in behaviours.OfType<VRCAvatarParameterDriver>()
                         .SelectMany(behaviour => behaviour.parameters))
                parameter.name = _parameterDic.ContainsKey(parameter.name)
                    ? _parameterDic[parameter.name]
                    : parameter.name;

            state.behaviours = behaviours.ToArray();
#endif

            var transitions = state.transitions.ToList();
            foreach (var transition in transitions)
            {
                var conditions = new List<AnimatorCondition>();
                foreach (var condition in transition.conditions)
                    conditions.Add(new AnimatorCondition
                    {
                        mode = condition.mode,
                        parameter = _parameterDic.ContainsKey(condition.parameter)
                            ? _parameterDic[condition.parameter]
                            : condition.parameter,
                        threshold = condition.threshold
                    });

                transition.conditions = conditions.ToArray();
            }

            state.transitions = transitions.ToArray();

            if (state.speedParameterActive)
                state.speedParameter = _parameterDic.ContainsKey(state.speedParameter)
                    ? _parameterDic[state.speedParameter]
                    : state.speedParameter;

            if (state.mirrorParameterActive)
                state.mirrorParameter = _parameterDic.ContainsKey(state.mirrorParameter)
                    ? _parameterDic[state.mirrorParameter]
                    : state.mirrorParameter;

            if (state.timeParameterActive)
                state.timeParameter = _parameterDic.ContainsKey(state.timeParameter)
                    ? _parameterDic[state.timeParameter]
                    : state.timeParameter;

            if (state.cycleOffsetParameterActive)
                state.cycleOffsetParameter = _parameterDic.ContainsKey(state.cycleOffsetParameter)
                    ? _parameterDic[state.cycleOffsetParameter]
                    : state.cycleOffsetParameter;

            if (state.motion != null) state.motion = MotionObfuscator(state.motion, root);

            return state;
        }

        private Motion MotionObfuscator(Motion motion, KannaProteccRoot root)
        {
            if (motion == null) return motion;

            switch (motion)
            {
                case AnimationClip clip:
                    {
                        if (AssetDatabase.GetAssetPath(motion).StartsWith("Assets/VRCSDK/")) return motion;
                        var animationClip = CopyAssetFile("anim", clip, root);
                        animationClip.name = GetAssetName(animationClip);
                        _animClipList.Add(animationClip);
                        return animationClip;
                    }
                case BlendTree tree:
                    {
                        var blendTree = CopyAssetFile("asset", tree, root);
                        blendTree.name = GetAssetName(blendTree);

                        blendTree.children = blendTree.children.Select(child => new ChildMotion
                        {
                            mirror = child.mirror,
                            motion = MotionObfuscator(child.motion, root),
                            position = child.position,
                            threshold = child.threshold,
                            cycleOffset = child.cycleOffset,
                            timeScale = child.timeScale,
                            directBlendParameter = _parameterDic.ContainsKey(child.directBlendParameter)
                                ? _parameterDic[child.directBlendParameter]
                                : child.directBlendParameter
                        }).ToArray();

                        blendTree.blendParameter = _parameterDic.ContainsKey(blendTree.blendParameter)
                            ? _parameterDic[blendTree.blendParameter]
                            : blendTree.blendParameter;
                        blendTree.blendParameterY = _parameterDic.ContainsKey(blendTree.blendParameterY)
                            ? _parameterDic[blendTree.blendParameterY]
                            : blendTree.blendParameterY;

                        return blendTree;
                    }
                default:
                    return motion;
            }
        }

        private static void CreateFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            if (folderPath[folderPath.Length - 1] == '/') folderPath = folderPath.Substring(0, folderPath.Length - 1);

            var names = folderPath.Split('/');
            for (var i = 1; i < names.Length; i++)
            {
                var parent = string.Join("/", names.Take(i).ToArray());
                var target = string.Join("/", names.Take(i + 1).ToArray());
                var child = names[i];
                if (!AssetDatabase.IsValidFolder(target)) AssetDatabase.CreateFolder(parent, child);
            }
        }

        private T CopyAssetFile<T>(string ext, T original, KannaProteccRoot root) where T : Object
        {
            var originalPath = AssetDatabase.GetAssetPath(original);
            if (string.IsNullOrEmpty(originalPath) || AssetDatabase.IsSubAsset(original) ||
                !AssetDatabase.IsMainAsset(original))
                return original;

            string newPath;
            if (!_filePathDic.ContainsKey(originalPath))
            {
                newPath = root.path + "/" + GUID.Generate() + "." + ext;
                while (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(newPath)))
                    newPath = root.path + "/" + GUID.Generate() + ".asset";

                AssetDatabase.CopyAsset(originalPath, newPath);
                _filePathDic.Add(originalPath, newPath);
            }
            else newPath = _filePathDic[originalPath];

            var asset = AssetDatabase.LoadAssetAtPath<T>(newPath);
            return asset;
        }

        private string GetAssetName<T>(T asset) where T : Object
        {
            string fileName = null;
            if (asset != null && !AssetDatabase.IsSubAsset(asset) && AssetDatabase.IsMainAsset(asset))
            {
                var assetPath = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(assetPath))
                    fileName = Path.GetFileName(assetPath);

                if (string.IsNullOrEmpty(fileName)) return fileName;
            }

            fileName = GUID.Generate() + "_" + _tempIndex;
            _tempIndex++;
            return fileName;
        }

        public void ClearObfuscatedFiles(KannaProteccRoot root)
        {
            if (string.IsNullOrEmpty(root.path)) return;

            FileUtil.DeleteFileOrDirectory(root.path);
            root.path = "";

            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();
        }

        public void ClearAllObfuscatedFiles(KannaProteccRoot root)
        {
            FileUtil.DeleteFileOrDirectory(root.pathPrefix);
            root.path = "";

            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();
        }

        private static void ProgressBar(string info, float min, float max = 12)
        {
            EditorUtility.DisplayProgressBar("Obfuscator", info, min / max);
        }
    }
}
#endif