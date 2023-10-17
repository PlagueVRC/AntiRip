#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

using UnityEngine;

using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

// A huge thank you to kamyu! This module would not be possible without them!

namespace Kanna.Protecc
{
    public class Obfuscator
    {
        public class Mapping
        {
            public List<(string, string, string)> RenamedValues = new List<(string, string, string)>();
        }

        public static Mapping mapping = new Mapping();

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
        private List<string> IgnoredParams = new List<string>();

        public GameObject Obfuscate(GameObject gobj, KannaProteccRoot root)
        {
            GameObject obj = null;

            KannaLogger.LogToFile($"Obfuscation Init..", KannaProteccRoot.LogLocation);

            _parameterDic.Clear();
            _objectNameDic.Clear();
            _filePathDic.Clear();
            _animClipList.Clear();
            _excludeNameSet.Clear();
            IgnoredParams.Clear();
            ObfuscatedMenus.Clear();
            mapping.RenamedValues.Clear();
            GC.Collect();

            try
            {
                if (string.IsNullOrEmpty(root.path))
                    root.path = root.pathPrefix + root.gameObject.name.Trim();

                KannaLogger.LogToFile($"Creating Obfuscated Files Folder..", KannaProteccRoot.LogLocation);
                CreateFolder(root.path);

                KannaLogger.LogToFile($"Cloning Avatar Object..", KannaProteccRoot.LogLocation);
                if (ProgressBar("Clone Avatar Object", 1))
                {
                    throw new OperationCanceledException();
                }
                var o = gobj;
                var gameObjectName = o.name.Trim() + "_Obfuscated";
                obj = Object.Instantiate(o);
                obj.name = gameObjectName;
                obj.SetActive(true);
                o.SetActive(false);

                KannaLogger.LogToFile($"Removing AnimatorController From Root Animator..", KannaProteccRoot.LogLocation);
                if (ProgressBar("Remove AnimatorController From Root Animator", 2))
                {
                    throw new OperationCanceledException();
                }
                var rootAnimator = obj.GetComponent<Animator>();
                if (rootAnimator != null) rootAnimator.runtimeAnimatorController = null;

                KannaLogger.LogToFile($"Finding VRCAvatarDescriptor Component..", KannaProteccRoot.LogLocation);

                if (ProgressBar("Find VRCAvatarDescriptor Component", 3))
                {
                    throw new OperationCanceledException();
                }

                var avatar = obj.GetComponent<VRCAvatarDescriptor>();

                KannaLogger.LogToFile($"Beginning ExpressionParameters Obfuscation..", KannaProteccRoot.LogLocation);

                if (ProgressBar("ExpressionParameters Obfuscation", 4))
                {
                    throw new OperationCanceledException();
                }

                if (avatar.expressionParameters != null)
                {
                    avatar.expressionParameters = ExpressionParametersObfuscator(avatar.expressionParameters, root);
                    EditorUtility.SetDirty(avatar.expressionParameters);
                }

                Utilities.ResetRandomizer();

                KannaLogger.LogToFile($"Beginning baseAnimationLayers Animator Obfuscation..", KannaProteccRoot.LogLocation);

                if (ProgressBar("BaseAnimationLayers AnimatorController Obfuscation", 5))
                {
                    throw new OperationCanceledException();
                }
                var animationLayers = avatar.baseAnimationLayers;
                for (var i = 0; i < animationLayers.Length; ++i)
                {
                    if (animationLayers[i].animatorController == null)
                        continue;

                    var excluded = ((AnimatorController)animationLayers[i].animatorController).name.ToLower().Contains("gogo") ||
                                   root.excludeAnimatorLayers.Any(p => (int)p == (int)animationLayers[i].type) ||
                                   root.excludeObjectNames.Any(z => z == (AnimatorController)animationLayers[i].animatorController);

                    var animator = (AnimatorController)animationLayers[i].animatorController;

                    if (excluded)
                    {
                        IgnoredParams.AddRange(animator.parameters.Select(s => s.name));
                        IgnoredParams = IgnoredParams.Distinct().ToList(); // Remove Duplicates
                        continue;
                    }

                    animationLayers[i].animatorController = AnimatorObfuscator(animator, root);
                    EditorUtility.SetDirty(animationLayers[i].animatorController);
                }

                avatar.baseAnimationLayers = animationLayers;

                Utilities.ResetRandomizer();

                KannaLogger.LogToFile($"Beginning specialAnimationLayers Animator Obfuscation..", KannaProteccRoot.LogLocation);

                if (ProgressBar("SpecialAnimationLayers AnimatorController Obfuscation", 6))
                {
                    throw new OperationCanceledException();
                }
                var specialAnimationLayers = avatar.specialAnimationLayers;
                for (var i = 0; i < specialAnimationLayers.Length; ++i)
                {
                    if (specialAnimationLayers[i].animatorController == null)
                        continue;

                    var excluded = ((AnimatorController)specialAnimationLayers[i].animatorController).name.ToLower().Contains("gogo") ||
                                   root.excludeAnimatorLayers.Any(p => (int)p == (int)specialAnimationLayers[i].type) ||
                                   root.excludeObjectNames.Any(z => z == (AnimatorController)specialAnimationLayers[i].animatorController);


                    var animator = (AnimatorController)specialAnimationLayers[i].animatorController;

                    if (excluded)
                    {
                        IgnoredParams.AddRange(animator.parameters.Select(s => s.name));
                        IgnoredParams = IgnoredParams.Distinct().ToList(); // Remove Duplicates
                        continue;
                    }

                    specialAnimationLayers[i].animatorController = AnimatorObfuscator(animator, root);
                    EditorUtility.SetDirty(specialAnimationLayers[i].animatorController);
                }

                avatar.specialAnimationLayers = specialAnimationLayers;

                Utilities.ResetRandomizer();

                KannaLogger.LogToFile($"Beginning Generic Animator Obfuscation..", KannaProteccRoot.LogLocation);

                if (ProgressBar("Misc Animators Obfuscation", 7))
                {
                    throw new OperationCanceledException();
                }
                var otherAnimators = obj.GetComponentsInChildren<Animator>(true)
                    .Where(t => t.runtimeAnimatorController == null || t.gameObject != obj);
                foreach (var animator in otherAnimators)
                {
                    if (animator.runtimeAnimatorController == null) continue;

                    var excluded = ((AnimatorController)animator.runtimeAnimatorController).name.ToLower().Contains("gogo") || root.excludeObjectNames.Any(z => z == (AnimatorController)animator.runtimeAnimatorController);

                    if (excluded)
                    {
                        IgnoredParams.AddRange(((AnimatorController)animator.runtimeAnimatorController).parameters.Select(s => s.name));
                        IgnoredParams = IgnoredParams.Distinct().ToList(); // Remove Duplicates
                        continue;
                    }

                    animator.runtimeAnimatorController = AnimatorObfuscator((AnimatorController)animator.runtimeAnimatorController, root);

                    EditorUtility.SetDirty(animator.runtimeAnimatorController);
                }

                // Update Thingies
                var AllContactReceivers = obj.GetComponentsInChildren<VRCContactReceiver>(true);
                var AllPhysBones = obj.GetComponentsInChildren<VRCPhysBone>(true);

                foreach (var thing in AllContactReceivers)
                {
                    if (thing != null && !string.IsNullOrEmpty(thing.parameter?.RemoveAllPhysBone()) && _parameterDic.ContainsKey(thing.parameter.RemoveAllPhysBone()))
                    {
                        thing.parameter = _parameterDic[thing.parameter.RemoveAllPhysBone()];
                    }
                }

                foreach (var thing in AllPhysBones)
                {
                    if (thing != null && !string.IsNullOrEmpty(thing.parameter))
                    {
                        if (thing != null && !string.IsNullOrEmpty(thing.parameter?.RemoveAllPhysBone()) && _parameterDic.ContainsKey(thing.parameter.RemoveAllPhysBone()))
                        {
                            thing.parameter = _parameterDic[thing.parameter.RemoveAllPhysBone()];
                        }
                    }
                }

                Utilities.ResetRandomizer();

                KannaLogger.LogToFile($"Beginning ExpressionsMenu Obfuscation..", KannaProteccRoot.LogLocation);

                if (ProgressBar("ExpressionsMenu Obfuscation", 8))
                {
                    throw new OperationCanceledException();
                }
                if (avatar.expressionsMenu != null)
                {
                    avatar.expressionsMenu = ExpressionsMenuObfuscator(avatar.expressionsMenu, root);
                    EditorUtility.SetDirty(avatar.expressionsMenu);
                }

                Utilities.ResetRandomizer();

                KannaLogger.LogToFile($"Saving Assets..", KannaProteccRoot.LogLocation);

                AssetDatabase.SaveAssets();

                KannaLogger.LogToFile($"Caching All Bones Recursively Via Animators", KannaProteccRoot.LogLocation);
                if (ProgressBar("Caching All Bones From Animator To Ignore Them In Rename", 9))
                {
                    throw new OperationCanceledException();
                }
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

                KannaLogger.LogToFile($"Beginning Object Name Obfuscation..", KannaProteccRoot.LogLocation);

                if (!root.disableObjectNameObfuscation)
                {
                    foreach (var objectName in root.excludeObjectNames.Where(item => item != null && !string.IsNullOrWhiteSpace(item.name)))
                    {
                        _excludeNameSet.Add(objectName.name);
                    }


                    KannaLogger.LogToFile($"Getting Every Transform Recursively..", KannaProteccRoot.LogLocation);
                    var children = obj.GetComponentsInChildren<Transform>(true).Where(t => t != obj.transform).ToList();

                    KannaLogger.LogToFile($"Going Through Transforms Individually, Ignoring Root Object And Excluded Objects..", KannaProteccRoot.LogLocation);

                    var ToRename = children.Select(child => child.gameObject)
                        .Where(childObject => childObject.GetInstanceID() != obj.GetInstanceID() &&
                                              !_excludeNameSet.Contains(childObject.name)).ToArray();

                    if (ProgressBar($"Obfuscating 0/{ToRename.Length} Transform Names", 10))
                    {
                        throw new OperationCanceledException();
                    }

                    for (var index = 0; index < ToRename.Length; index++)
                    {
                        var childObject = ToRename[index];

                        if (ProgressBar($"Obfuscating {index + 1}/{ToRename.Length} Transform Names", 10))
                        {
                            throw new OperationCanceledException();
                        }

                        if (!_objectNameDic.ContainsKey(childObject.name))
                        {
                            KannaLogger.LogToFile($"Generating New Name For {childObject.name}..", KannaProteccRoot.LogLocation);

                            var newName = Utilities.GenerateRandomUniqueName(false);
                            while (_objectNameDic.ContainsKey(newName))
                                newName = Utilities.GenerateRandomUniqueName(false);

                            _objectNameDic.Add(childObject.name, newName);
                            mapping.RenamedValues.Add(("Object", childObject.name, newName));
                        }

                        childObject.name = _objectNameDic[childObject.name];
                    }

                    KannaLogger.LogToFile($"Beginning Updating Of AnimationClips; Cached Previously From Animator Obfuscations..", KannaProteccRoot.LogLocation);

                    if (ProgressBar($"Updating 0/{_animClipList.Count} AnimationClips", 11))
                    {
                        throw new OperationCanceledException();
                    }
                    for (var index = 0; index < _animClipList.Count; index++)
                    {
                        var clip = _animClipList[index];

                        if (ProgressBar($"Updating {index + 1}/{_animClipList.Count} AnimationClips", 11))
                        {
                            throw new OperationCanceledException();
                        }

                        var array = AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)).ToArray();
                        for (var index1 = 0; index1 < array.Length; index1++)
                        {
                            var binding = array[index1];

                            if (ProgressBar($"Updating {index + 1}/{_animClipList.Count} ({index1}/{array.Length}) AnimationClips", 11))
                            {
                                throw new OperationCanceledException();
                            }

                            var copy = binding;
                            var bindingPath = binding.path;

                            var names = bindingPath.Split('/');
                            for (var i = 0; i < names.Length; i++)
                                names[i] = _objectNameDic.ContainsKey(names[i])
                                    ? _objectNameDic[names[i]]
                                    : names[i];

                            copy.path = string.Join("/", names);

                            var objcurve = AnimationUtility.GetObjectReferenceCurve(clip, binding);

                            if (objcurve != null && objcurve.Length > 0)
                            {
                                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                                AnimationUtility.SetObjectReferenceCurve(clip, copy, objcurve);
                            }
                            else
                            {
                                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                                AnimationUtility.SetEditorCurve(clip, binding, null);
                                AnimationUtility.SetEditorCurve(clip, copy, curve);
                            }
                        }

                        EditorUtility.SetDirty(clip);

                        KannaLogger.LogToFile($"Finished AnimClip Bindings For Clip: {clip.name}", KannaProteccRoot.LogLocation);
                    }
                }

                KannaLogger.LogToFile($"Randomizing Sibling Order..", KannaProteccRoot.LogLocation);
                if (ProgressBar("Randomizing Sibling Order", 12))
                {
                    throw new OperationCanceledException();
                }

                RandomizeAllSiblingOrders(obj);

                KannaLogger.LogToFile($"Obfuscation Finished..", KannaProteccRoot.LogLocation);

                File.WriteAllText("ObfuscationMapping.json", JsonConvert.SerializeObject(mapping, Formatting.Indented));

                EditorSceneManager.MarkAllScenesDirty();

                ProgressBar("Done!", 1, 1);
            }
            catch (Exception err)
            {
                if (err is OperationCanceledException)
                {
                    obj = null;
                }

                KannaLogger.LogToFile($"{err}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Error);
            }
            finally
            {
                KannaLogger.LogToFile($"Obfuscation Cleanup..", KannaProteccRoot.LogLocation);

                _parameterDic.Clear();
                _objectNameDic.Clear();
                _animClipList.Clear();
                _filePathDic.Clear();
                _excludeNameSet.Clear();

                EditorSceneManager.SaveOpenScenes();
                EditorUtility.ClearProgressBar();

                GC.Collect();
                AssetDatabase.ImportAsset(root.path, ImportAssetOptions.ImportRecursive);
            }

            return obj;
        }

        private void RandomizeAllSiblingOrders(GameObject obj)
        {
            var AllChildren = obj.transform.GetAllChildren(true);

            AllChildren.Remove(obj.transform);

            foreach (var child in AllChildren)
            {
                child.SetSiblingIndex(UnityEngine.Random.Range(0, child.parent.childCount));
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
            var expressionParameters = CopyAssetFile("asset", oldEp, root);

            var templist = expressionParameters.parameters.ToList();
            for (var i = 0; i < root._bitKeys.Length; i++)
            {
                KannaLogger.LogToFile($"Adding BitKey{i} To ExpressionParameters..", KannaProteccRoot.LogLocation);
                templist.Add(new VRCExpressionParameters.Parameter
                {
                    name = $"BitKey{i}",
                    saved = true,
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = 0f
                });
            }
            expressionParameters.parameters = templist.ToArray();

            EditorUtility.SetDirty(expressionParameters);

            KannaLogger.LogToFile($"Obfuscating Non-Skipped And Non-Ignored Parameters..", KannaProteccRoot.LogLocation);

            var parameters = expressionParameters.parameters.ToList().Where(p => !string.IsNullOrEmpty(p.name))
                .ToList();

            foreach (var parameter in parameters.Where(parameter => !SkipParameterNames.Contains(parameter.name) && root.excludeParamNames.All(o => !Regex.IsMatch(parameter.name, o)) && !IgnoredParams.Contains(parameter.name)))
            {
                if (_parameterDic.ContainsKey(parameter.name))
                {
                    parameter.name = _parameterDic[parameter.name];
                    continue;
                }

                var newName = Utilities.GenerateRandomUniqueName(true);
                while (_parameterDic.ContainsKey(newName)) newName = Utilities.GenerateRandomUniqueName(true);

                if (parameter.name.Contains("BitKey"))
                {
                    root.ParameterRenamedValues[parameter.name] = newName;
                }

                _parameterDic[parameter.name] = newName;
                mapping.RenamedValues.Add(("Parameter", parameter.name, newName));
                parameter.name = newName;
            }

            parameters = expressionParameters.parameters.ToList();
            //while (120 - expressionParameters.CalcTotalCost() > 0)
            //{
            //    var newName = Utilities.GenerateRandomUniqueName();
            //    while (_parameterDic.ContainsKey(newName)) newName = Utilities.GenerateRandomUniqueName();

            //    parameters.Add(new VRCExpressionParameters.Parameter
            //    {
            //        name = newName,
            //        saved = Random.Range(0, 2) == 0,
            //        valueType = VRCExpressionParameters.ValueType.Float,
            //        defaultValue = (float)(Math.Truncate((double)Random.Range(0f, 1f) * 100) / 100)
            //    });

            //    expressionParameters.parameters = parameters.ToArray();
            //}

            //while (128 - expressionParameters.CalcTotalCost() > 0)
            //{
            //    var newName = Utilities.GenerateRandomUniqueName();
            //    while (_parameterDic.ContainsKey(newName)) newName = Utilities.GenerateRandomUniqueName();

            //    parameters.Add(new VRCExpressionParameters.Parameter
            //    {
            //        name = newName,
            //        saved = Random.Range(0, 2) == 0,
            //        valueType = VRCExpressionParameters.ValueType.Bool,
            //        defaultValue = Random.Range(0, 2) == 0 ? 1 : 0
            //    });

            //    expressionParameters.parameters = parameters.ToArray();
            //}

            var AmountToReserveForOtherThings = 75;

            KannaLogger.LogToFile($"Done, Padding Parameters With Dummy Parameters, Reserving {AmountToReserveForOtherThings}..", KannaProteccRoot.LogLocation);

            while ((VRCExpressionParameters.MAX_PARAMETER_COST - (expressionParameters.CalcTotalCost() + AmountToReserveForOtherThings)) > 0)
            {
                var newName = Utilities.GenerateRandomUniqueName(true);

                while (_parameterDic.ContainsKey(newName))
                    newName = Utilities.GenerateRandomUniqueName(true);

                parameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = newName,
                    saved = true,
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = Random.Range(0, 2) == 0 ? 1 : 0
                });

                expressionParameters.parameters = parameters.ToArray();
            }

            parameters = parameters.OrderBy(a => Guid.NewGuid()).ToList();
            expressionParameters.parameters = parameters.ToArray();

            EditorUtility.SetDirty(expressionParameters);

            KannaLogger.LogToFile($"ExpressionParameters Obfuscation Finished", KannaProteccRoot.LogLocation);

            AssetDatabase.SaveAssets();

            return expressionParameters;
        }

        private List<VRCExpressionsMenu> ObfuscatedMenus = new List<VRCExpressionsMenu>();

        private VRCExpressionsMenu ExpressionsMenuObfuscator(VRCExpressionsMenu menu, KannaProteccRoot root)
        {
            ObfuscatedMenus.Add(menu);

            var expressionsMenu = CopyAssetFile("asset", menu, root);

            for (var i = 0; i < expressionsMenu.controls.Count; i++)
            {
                var control = expressionsMenu.controls[i];

                if (!string.IsNullOrEmpty(control.parameter?.name) && _parameterDic.TryGetValue(control.parameter.name, out var value))
                {
                    expressionsMenu.controls[i].parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = value
                    };
                }

                if (control.subParameters != null)
                    for (var index = 0; index < control.subParameters.Length; index++)
                    {
                        var param = control.subParameters[index];

                        if (string.IsNullOrEmpty(param.name) || !_parameterDic.TryGetValue(param.name, out var value2))
                        {
                            continue;
                        }

                        control.subParameters[index] = new VRCExpressionsMenu.Control.Parameter
                        {
                            name = value2
                        };
                    }

                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null && control.subMenu != expressionsMenu && control.subMenu != menu && !_filePathDic.ContainsValue(AssetDatabase.GetAssetPath(control.subMenu)))
                {
                    control.subMenu = ExpressionsMenuObfuscator(control.subMenu, root);
                }
            }

            EditorUtility.SetDirty(expressionsMenu);

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
                if (_parameterDic.ContainsKey(t.name.RemoveAllPhysBone()))
                {
                    t.name = _parameterDic[t.name.RemoveAllPhysBone()] + t.name.GetPhysBoneEnding();
                }
                else if (Array.FindIndex(SkipParameterNames, value => value == t.name) == -1 && root.excludeParamNames.All(o => !Regex.IsMatch(t.name.RemoveAllPhysBone(), o)) && !IgnoredParams.Contains(t.name.RemoveAllPhysBone()))
                {
                    var newName = Utilities.GenerateRandomUniqueName(true);
                    while (_parameterDic.ContainsKey(newName)) newName = Utilities.GenerateRandomUniqueName(true);

                    mapping.RenamedValues.Add(($"Animator Parameter", t.name.RemoveAllPhysBone(), newName));
                    _parameterDic.Add(t.name.RemoveAllPhysBone(), newName);
                    t.name = newName + t.name.GetPhysBoneEnding();
                }
            }

            parameters = parameters.OrderBy(a => Guid.NewGuid()).ToList();
            animator.parameters = parameters.ToArray();

            var layers = animator.layers.ToList();

            foreach (var layer in layers)
            {
                if (layer?.stateMachine == null)
                {
                    continue;
                }

                var newLayerName = Utilities.GenerateRandomUniqueName(false);
                mapping.RenamedValues.Add(($"Animator Layer", layer.name, newLayerName));
                layer.name = newLayerName;
                layer.stateMachine = StateMachineObfuscator(layer.name, layer.stateMachine, root);
            }

            animator.layers = layers.ToArray();

            EditorUtility.SetDirty(animator);

            return animator;
        }

        public void ObfuscateLayer(AnimatorControllerLayer layer, AnimatorController controller, KannaProteccRoot root)
        {
            if (layer == null || controller == null) return;

            var parameters = controller.parameters.ToList();
            foreach (var t in parameters)
            {
                if (_parameterDic.ContainsKey(t.name.RemoveAllPhysBone()))
                {
                    t.name = _parameterDic[t.name.RemoveAllPhysBone()] + t.name.GetPhysBoneEnding();
                }
                else if (t.name.Contains("BitKey"))
                {
                    var newName = root.ParameterRenamedValues[t.name];

                    _parameterDic.Add(t.name, newName);

                    t.name = newName;
                }
            }

            parameters = parameters.OrderBy(a => Guid.NewGuid()).ToList();
            controller.parameters = parameters.ToArray();

            var layers = controller.layers.ToList();

            var newLayerName = Utilities.GenerateRandomUniqueName(false);

            var index = layers.FindIndex(o => o.name == layer.name);

            layer.name = newLayerName;
            layer.stateMachine = StateMachineObfuscator(layer.name, layer.stateMachine, root);

            layers[index] = layer;

            controller.layers = layers.ToArray();

            EditorUtility.SetDirty(controller);

            AssetDatabase.SaveAssets();
        }

        private ChildAnimatorStateMachine ChildStateMachineObfuscator(ChildAnimatorStateMachine stateMachine, KannaProteccRoot root)
        {
            var newName = Utilities.GenerateRandomUniqueName(false);
            return new ChildAnimatorStateMachine
            {
                position = new Vector3(float.NaN, float.NaN, float.NaN),
                stateMachine = StateMachineObfuscator(newName, stateMachine.stateMachine, root),
            };
        }

        private AnimatorStateMachine StateMachineObfuscator(string stateMachineName, AnimatorStateMachine stateMachine, KannaProteccRoot root)
        {
            mapping.RenamedValues.Add(($"State Machine", stateMachine.name, stateMachineName));
            stateMachine.name = stateMachineName;

#if VRC_SDK_VRCSDK3
            var behaviours = stateMachine.behaviours.ToList();
            foreach (var parameter in behaviours.OfType<VRCAvatarParameterDriver>()
                         .SelectMany(behaviour => behaviour.parameters))
                parameter.name = _parameterDic.TryGetValue(parameter.name.RemoveAllPhysBone(), out var value)
                    ? value + parameter.name.GetPhysBoneEnding()
                    : parameter.name;

            stateMachine.behaviours = behaviours.ToArray();
#endif

            stateMachine.anyStatePosition = new Vector3(float.NaN, float.NaN, float.NaN);
            stateMachine.exitPosition = new Vector3(float.NaN, float.NaN, float.NaN);
            stateMachine.parentStateMachinePosition = new Vector3(float.NaN, float.NaN, float.NaN);
            stateMachine.entryPosition = new Vector3(float.NaN, float.NaN, float.NaN);

            var childStates = new List<ChildAnimatorState>();

            foreach (var t in stateMachine.states)
            {
                childStates.Add(
                    new ChildAnimatorState
                    {
                        position = new Vector3(float.NaN, float.NaN, float.NaN),
                        state = AnimatorStateObfuscator(t.state, root)
                    }
                );
            }

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
                        parameter = _parameterDic.TryGetValue(condition.parameter.RemoveAllPhysBone(), out var value)
                            ? value + condition.parameter.GetPhysBoneEnding()
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
                        parameter = _parameterDic.TryGetValue(condition.parameter.RemoveAllPhysBone(), out var value)
                            ? value + condition.parameter.GetPhysBoneEnding()
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
            state.name = Utilities.GenerateRandomUniqueName(false);

#if VRC_SDK_VRCSDK3
            var behaviours = state.behaviours.ToList();
            foreach (var parameter in behaviours.OfType<VRCAvatarParameterDriver>()
                         .SelectMany(behaviour => behaviour.parameters))
                parameter.name = _parameterDic.TryGetValue(parameter.name.RemoveAllPhysBone(), out var value)
                    ? value + parameter.name.GetPhysBoneEnding()
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
                        parameter = _parameterDic.TryGetValue(condition.parameter.RemoveAllPhysBone(), out var value)
                            ? value + condition.parameter.GetPhysBoneEnding()
                            : condition.parameter,
                        threshold = condition.threshold
                    });

                transition.conditions = conditions.ToArray();
            }

            state.transitions = transitions.ToArray();

            if (state.speedParameterActive)
                state.speedParameter = _parameterDic.TryGetValue(state.speedParameter.RemoveAllPhysBone(), out var value)
                    ? value + state.speedParameter.GetPhysBoneEnding()
                    : state.speedParameter;

            if (state.mirrorParameterActive)
                state.mirrorParameter = _parameterDic.TryGetValue(state.mirrorParameter.RemoveAllPhysBone(), out var value)
                    ? value + state.mirrorParameter.GetPhysBoneEnding()
                    : state.mirrorParameter;

            if (state.timeParameterActive)
                state.timeParameter = _parameterDic.TryGetValue(state.timeParameter.RemoveAllPhysBone(), out var value)
                    ? value + state.timeParameter.GetPhysBoneEnding()
                    : state.timeParameter;

            if (state.cycleOffsetParameterActive)
                state.cycleOffsetParameter = _parameterDic.TryGetValue(state.cycleOffsetParameter.RemoveAllPhysBone(), out var value)
                    ? value + state.cycleOffsetParameter.GetPhysBoneEnding()
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
                        if (AssetDatabase.GetAssetPath(motion).ToLower().Contains("proxyanim")) 
                        {
                            return motion; // To Do: Figure Out Why This Is Needed, Why Do Proxy Anims Break?
                        }

                        var animationClip = CopyAssetFile("anim", clip, root);
                        animationClip.name = GetAssetName(animationClip);
                        _animClipList.Add(animationClip);
                        EditorUtility.SetDirty(animationClip);
                        return animationClip;
                    }
                case BlendTree tree:
                    {
                        var blendTree = CopyAssetFile("asset", tree, root);
                        blendTree.name = GetAssetName(blendTree);

                        var children = new List<ChildMotion>();
                        children.AddRange(blendTree.children);

                        for (var index = 0; index < children.Count; index++)
                        {
                            var child = children[index];

                            child.motion = MotionObfuscator(child.motion, root);
                            child.directBlendParameter = _parameterDic.TryGetValue(child.directBlendParameter.RemoveAllPhysBone(), out var value)
                                ? value + child.directBlendParameter.GetPhysBoneEnding()
                                : child.directBlendParameter;

                            children[index] = child;
                        }

                        blendTree.children = children.ToArray();

                        blendTree.blendParameter = _parameterDic.TryGetValue(blendTree.blendParameter.RemoveAllPhysBone(), out var value1)
                            ? value1 + blendTree.blendParameter.GetPhysBoneEnding()
                            : blendTree.blendParameter;
                        blendTree.blendParameterY = _parameterDic.TryGetValue(blendTree.blendParameterY.RemoveAllPhysBone(), out var value2)
                            ? value2 + blendTree.blendParameterY.GetPhysBoneEnding()
                            : blendTree.blendParameterY;

                        return blendTree;
                    }
                default:
                    return motion;
            }
        }

        public static void CreateFolder(string folderPath)
        {
            KannaLogger.LogToFile($"Creating Folder: {folderPath}", KannaProteccRoot.LogLocation);

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
            {
                KannaLogger.LogToFile($"Ignoring Asset: {original.name}: No Path, Is Sub Asset Or Is Main Asset!", KannaProteccRoot.LogLocation);
                return original;
            }

            KannaLogger.LogToFile($"Copying Asset: {originalPath}", KannaProteccRoot.LogLocation);

            string newPath;
            if (!_filePathDic.ContainsKey(originalPath)) // Gen File
            {
                newPath = root.path + "/" + GUID.Generate() + "." + ext;
                while (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(newPath)))
                    newPath = root.path + "/" + GUID.Generate() + ext;

                mapping.RenamedValues.Add(($"Asset: {ext}", Path.GetFileNameWithoutExtension(originalPath), Path.GetFileNameWithoutExtension(newPath)));

                AssetDatabase.CopyAsset(originalPath, newPath);
                _filePathDic[originalPath] = newPath;
            }
            else newPath = _filePathDic[originalPath]; // Use Existing

            var asset = AssetDatabase.LoadAssetAtPath<T>(newPath);

            if (asset == null)
            {
                KannaLogger.LogToFile($"blyat, {newPath} no existo when loaded (CopyAsset Was Done, Then LoadAssetAtPath, Yet The Loaded Asset Was Null)", KannaProteccRoot.LogLocation, KannaLogger.LogType.Error);
            }

            EditorUtility.SetDirty(asset);

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

            fileName = GUID.Generate().ToString();

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

        private static bool ProgressBar(string info, float min, float max = 12)
        {
            return EditorUtility.DisplayCancelableProgressBar("Obfuscator", info, min / max);
        }
    }
}

public static class ObfuscatorExt
{
    public static string RemoveAllPhysBone(this string text)
    {
        return text.RemoveAll(new[] { "_IsGrabbed", "_IsPosed", "_Angle", "_Stretch", "_Squish" });
    }

    public static string GetPhysBoneEnding(this string text)
    {
        return text.GetEndingMatch(new[] { "_IsGrabbed", "_IsPosed", "_Angle", "_Stretch", "_Squish" });
    }

    public static string RemoveAll(this string text, IEnumerable<string> words)
    {
        var result = text;

        foreach (var word in words)
        {
            result = result.Replace(word, "");
        }

        return result;
    }

    public static string GetEndingMatch(this string text, IEnumerable<string> words)
    {
        foreach (var word in words)
        {
            if (text.EndsWith(word))
            {
                return word;
            }
        }

        return "";
    }
}
#endif
