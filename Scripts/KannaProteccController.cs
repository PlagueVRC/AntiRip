#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerLayer = UnityEditor.Animations.AnimatorControllerLayer;
using Object = UnityEngine.Object;

namespace Kanna.Protecc
{
    public class KannaProteccController
    {
        string[] _KannaProteccKeyNames;
        AnimationClip[] _clipsFalse;
        AnimationClip[] _clipsTrue;

        const string StateMachineName = "KannaProteccKey{0} State Machine";
        const string BlendTreeName = "KannaProteccKey{0} Blend Tree";
        const string BitKeySwitchName = "KannaProteccKey{0}{1} BitKey Switch";
        const string BitKeySwitchTransitionName = "KannaProteccKey{0}{1} BitKey Switch Transition";
        const string TrueLabel = "True";
        const string FalseLabel = "False";
        
        public void InitializeCount(int count)
        {
            _clipsFalse = new AnimationClip[count];
            _clipsTrue = new AnimationClip[count];
            _KannaProteccKeyNames = new string[count];
            for (var i = 0; i < _KannaProteccKeyNames.Length; ++i)
            {
                _KannaProteccKeyNames[i] = $"BitKey{i}";
            }
        }
        
        public void ValidateAnimations(GameObject gameObject, AnimatorController controller)
        {
            for (var i = 0; i < _KannaProteccKeyNames.Length; ++i)
            {
                ValidateClip(gameObject, controller, i);
            }

            var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(true).Where(o => o.materials.Any(a => KannaProteccMaterial.IsShaderSupported(a.shader, out _))).ToArray();
            foreach (var meshRenderer in meshRenderers)
            {
                for (var i = 0; i < _clipsFalse.Length; ++i)
                {
                    var transformPath = AnimationUtility.CalculateTransformPath(meshRenderer.transform, gameObject.transform);
                    _clipsFalse[i].SetCurve(transformPath, typeof(MeshRenderer), $"material._BitKey{i}", new AnimationCurve(new Keyframe(0, 0)));
                    _clipsTrue[i].SetCurve(transformPath, typeof(MeshRenderer), $"material._BitKey{i}", new AnimationCurve(new Keyframe(0, 1)));
                }
            }

            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(o => o.materials.Any(a => KannaProteccMaterial.IsShaderSupported(a.shader, out _))).ToArray();
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                for (var i = 0; i < _clipsFalse.Length; ++i)
                {
                    var transformPath = AnimationUtility.CalculateTransformPath(skinnedMeshRenderer.transform,gameObject.transform);
                    _clipsFalse[i].SetCurve(transformPath, typeof(SkinnedMeshRenderer), $"material._BitKey{i}", new AnimationCurve(new Keyframe(0, 0)));
                    _clipsTrue[i].SetCurve(transformPath, typeof(SkinnedMeshRenderer), $"material._BitKey{i}", new AnimationCurve(new Keyframe(0, 1)));
                }
            }

            AssetDatabase.SaveAssets();
        }

        private void ValidateClip(GameObject gameObject, AnimatorController controller, int index)
        {
            var controllerPath = AssetDatabase.GetAssetPath(controller);
            var controllerFileName = System.IO.Path.GetFileName(controllerPath);
            
            var clipName = $"{gameObject.name}_{_KannaProteccKeyNames[index]}";
            var clipNameFalse = $"{clipName}_False";
            var clipNameFalseFile = $"{clipNameFalse}.anim";
            var clipNameTrue = $"{clipName}_True";
            var clipNameTrueFile = $"{clipNameTrue}.anim";
            var folderPath = controllerPath.Replace(controllerFileName, $"BitKeyClips");
            
            if (controller.animationClips.All(c => c.name != clipNameFalse))
            {
                _clipsFalse[index] = new AnimationClip()
                {
                    name = clipNameFalse
                };
                var clip0Path = controllerPath.Replace(controllerFileName, $"BitKeyClips/{clipNameFalseFile}");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                AssetDatabase.CreateAsset(_clipsFalse[index], clip0Path);
                AssetDatabase.SaveAssets();
                // Debug.Log($"Adding and Saving Clip: {clip0Path}");
            }
            else
            {
                _clipsFalse[index] = controller.animationClips.FirstOrDefault(c => c.name == clipNameFalse);
                // Debug.Log($"Found clip: {clipNameFalse}");
            }
            
            if (controller.animationClips.All(c => c.name != clipNameTrue))
            {
                _clipsTrue[index] = new AnimationClip()
                {
                    name = clipNameTrue
                };
                var clip100Path = controllerPath.Replace(controllerFileName, $"BitKeyClips/{clipNameTrueFile}");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                AssetDatabase.CreateAsset(_clipsTrue[index], clip100Path);
                AssetDatabase.SaveAssets();
                // Debug.Log($"Adding and Saving Clip: {clip100Path}");
            }
            else
            {
                _clipsTrue[index] = controller.animationClips.FirstOrDefault(c => c.name == clipNameTrue);
                // Debug.Log($"Found clip: {clipNameTrue}");
            }
        }

        public void ValidateParameters(AnimatorController controller)
        {
            foreach (var keyName in _KannaProteccKeyNames)
            {
                if (controller.parameters.All(parameter => parameter.name != keyName))
                {
                    controller.AddParameter(keyName, AnimatorControllerParameterType.Bool);
                    AssetDatabase.SaveAssets();
                    // Debug.Log($"Adding parameter: {keyName}");
                }
                else
                {
                    // Debug.Log($"Parameter already added: {keyName}");
                }
            }
        }

        public void ValidateLayers(AnimatorController controller)
        {
            for (var i = 0; i < _KannaProteccKeyNames.Length; ++i)
            {
                if (controller.layers.All(l => l?.name != _KannaProteccKeyNames[i]))
                {
                    CreateLayer(i, controller);
                }
                else
                {
                    var layerList = controller.layers.ToList();
                    var layers = layerList.FindAll(l => l.name == _KannaProteccKeyNames[i]);
                    if (layers.Count > 1)
                    {
                        // Debug.Log("Duplicate layers flushing!");
                        // Somehow it added multiple layers so flush all duplicates and remake
                        for (var l = layers.Count - 1; l > -1; --l)
                        {
                            var layerIndex = layerList.IndexOf(layers[l]);
                            if (layerIndex != -1)
                                controller.RemoveLayer(layerIndex);
                        }
                        CreateLayer(i, controller);
                    }
                    else if (layers.Count == 0)
                    {
                        // Debug.Log("Layer missing!");
                        CreateLayer(i, controller);
                    }
                    else if (layers[0].stateMachine == null)
                    {
                        // Debug.Log("Layer missing stateMachine!");
                        var layerIndex = layerList.IndexOf(layers[0]);
                        controller.RemoveLayer(layerIndex);
                        CreateLayer(i, controller);
                    }
                }
            }
        }
        
        public void ValidateBitKeySwitches(AnimatorController controller)
        {
            for (var i = 0; i < _KannaProteccKeyNames.Length; ++i)
            {
                var layer = controller.layers.FirstOrDefault(l => l.name == _KannaProteccKeyNames[i]);
                ValidateBitKeySwitch(i, layer, controller);
            }
        }
        
        private void ValidateBitKeySwitch(int index, AnimatorControllerLayer layer, AnimatorController controller)
        {
            var trueSwitchName = string.Format(BitKeySwitchName, "True", index);
            var falseSwitchName = string.Format(BitKeySwitchName, "False", index);
            
            if (layer.stateMachine.states.All(s => s.state.name != trueSwitchName))
            {
                // Debug.Log($"Layer missing BitKeySwtich. {trueSwitchName}");
                AddBitKeySwitchState(index, layer, controller, true);
            }
            else
            {
                // Debug.Log($"Layer BitKey Switch Validated {trueSwitchName}.");
                ValidateBitKeySwitchState(index, layer, controller, true);
            }
            
            if (layer.stateMachine.states.All(s => s.state.name != falseSwitchName))
            {
                // Debug.Log($"Layer missing BitKeySwtich. {falseSwitchName}");
                AddBitKeySwitchState(index, layer, controller, false);
            }
            else
            {
                // Debug.Log($"Layer BitKey Switch Validated {falseSwitchName}.");
                ValidateBitKeySwitchState(index, layer, controller, false);
            }
        }

        void CreateLayer(int index, AnimatorController controller)
        {
            // Debug.Log($"Creating layer: {_KannaProteccKeyNames[index]}");
            
            var controllerPath = AssetDatabase.GetAssetPath(controller);

            var layer = new AnimatorControllerLayer
            {
                name = _KannaProteccKeyNames[index],
                defaultWeight = 1,
                stateMachine = new AnimatorStateMachine
                {
                    name = string.Format(StateMachineName, index)
                },
            };

            controller.AddLayer(layer);
            AssetDatabase.AddObjectToAsset(layer.stateMachine, controllerPath);
            AssetDatabase.SaveAssets();
        }
        
        void ValidateBitKeySwitchState(int index, AnimatorControllerLayer layer, AnimatorController controller, bool switchState)
        {
            var switchName = string.Format(BitKeySwitchName, StateLabel(switchState), index);
            var switchTransitionName = string.Format(BitKeySwitchTransitionName, StateLabel(switchState), index);

            var state = layer.stateMachine.states.First(s => s.state.name == switchName).state;
            state.motion = switchState ? _clipsTrue[index] : _clipsFalse[index];
            state.speed = 1;
            
            var transition = layer.stateMachine.anyStateTransitions.First(t => t.destinationState == state);

            if (transition == null)
            {
                transition = layer.stateMachine.AddAnyStateTransition(state);
            }
            
            transition.name = switchTransitionName;
            transition.canTransitionToSelf = false;
            transition.duration = 0;

            if (transition.conditions == null || 
                transition.conditions.Length == 0 ||
                transition.conditions.Length > 1)
            {
                var falseCondition = new AnimatorCondition
                {
                    mode = switchState ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    parameter = _KannaProteccKeyNames[index],
                    threshold = 0
                };
                transition.conditions = new[] {falseCondition};
            }
            else
            {
                var condition = transition.conditions[0];
                condition.mode = switchState ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
                condition.parameter = _KannaProteccKeyNames[index];
                condition.threshold = 0;
            }

            AssetDatabase.SaveAssets();
        }
        
        void AddBitKeySwitchState(int index, AnimatorControllerLayer layer, AnimatorController controller, bool switchState)
        {
            var switchName = string.Format(BitKeySwitchName, StateLabel(switchState), index);
            var switchTransitionName = string.Format(BitKeySwitchTransitionName, StateLabel(switchState), index);
            
            var state = layer.stateMachine.AddState(switchName);
            state.motion = switchState ? _clipsTrue[index] : _clipsFalse[index];
            state.speed = 1;
            
            var condition = new AnimatorCondition
            {
                mode = switchState ?  AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                parameter = _KannaProteccKeyNames[index],
                threshold = 0
            };

            var transition = layer.stateMachine.AddAnyStateTransition(state);
            transition.name = switchTransitionName;
            transition.canTransitionToSelf = false;
            transition.duration = 0;
            transition.conditions = new[] {condition};
            
            AssetDatabase.SaveAssets();
        }

        string StateLabel(bool state) => state ? TrueLabel : FalseLabel;

        public void DeleteKannaProteccObjectsFromController(AnimatorController controller)
        {
            var controllerPath = AssetDatabase.GetAssetPath(controller);
            foreach (var subObject in AssetDatabase.LoadAllAssetsAtPath(controllerPath))
            {
                if (subObject != null && subObject.name.Contains("KannaProtecc"))
                {
                    AssetDatabase.RemoveObjectFromAsset(subObject);
                }
            }
            AssetDatabase.SaveAssets();
            
            foreach (var keyName in _KannaProteccKeyNames)
            {
                var layerList = controller.layers.ToList();
                layerList.RemoveAll(l => l.name == keyName);
                controller.layers = layerList.ToArray();
                
                var parametersList = controller.parameters.ToList();
                parametersList.RemoveAll(l => l.name == keyName);
                controller.parameters = parametersList.ToArray();
            }
        }
    }
}
#endif
