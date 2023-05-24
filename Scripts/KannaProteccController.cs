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
        private DBT_API.DBT_Instance DBT;

        string[] _KannaProteccKeyNames;
        AnimationClip[] _clipsFalse;
        AnimationClip[] _clipsTrue;

       public static string LayerName = "KannaProtecc";

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

            var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(true).Where(o => o?.sharedMaterials != null && o.sharedMaterials.Any(a => KannaProteccMaterial.IsShaderSupported(a.shader, out _))).ToArray();
            foreach (var meshRenderer in meshRenderers)
            {
                for (var i = 0; i < _clipsFalse.Length; ++i)
                {
                    var transformPath = AnimationUtility.CalculateTransformPath(meshRenderer.transform, gameObject.transform);
                    _clipsFalse[i].SetCurve(transformPath, typeof(MeshRenderer), $"material._BitKey{i}", new AnimationCurve(new Keyframe(0, 0)));
                    _clipsTrue[i].SetCurve(transformPath, typeof(MeshRenderer), $"material._BitKey{i}", new AnimationCurve(new Keyframe(0, 1)));
                }
            }

            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(o => o?.sharedMaterials != null && o.sharedMaterials.Any(a => KannaProteccMaterial.IsShaderSupported(a.shader, out _))).ToArray();
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
                    controller.AddParameter(keyName, AnimatorControllerParameterType.Float);
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
            if (controller.layers.All(l => l?.name != LayerName))
            {
                CreateLayer(LayerName, controller);
            }
        }

        void CreateLayer(string Name, AnimatorController controller)
        {
            DBT = new DBT_API.DBT_Instance(controller, Name);

            for (var i = 0; i < _KannaProteccKeyNames.Length; i++)
            {
                AddBitKeyState(i);
            }
        }
        
        void AddBitKeyState(int index)
        {
            DBT.masterTree.Trees.Add(new DBT_API.DBT_Instance.BlendState(DBT.masterTree, DBT.controller.parameters.First(o => o.name == _KannaProteccKeyNames[index]), _clipsFalse[index], _clipsTrue[index], $"BitKey{index}"));

            AssetDatabase.SaveAssets();
        }

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

            var layerList = controller.layers.ToList();
            var parametersList = controller.parameters.ToList();

            layerList.RemoveAll(l => l.name == LayerName);

            foreach (var keyName in _KannaProteccKeyNames)
            {
                layerList.RemoveAll(l => l.name == keyName);

                parametersList.RemoveAll(l => l.name == keyName);
            }

            controller.layers = layerList.ToArray();
            controller.parameters = parametersList.ToArray();
        }
    }
}

public class DBT_API
{
    public class DBT_Instance
    {
        public class BlendState
        {
            public AnimatorControllerParameter parameter;
            public BlendTree Tree;

            public BlendState(MasterTree Master, AnimatorControllerParameter parameter, AnimationClip OffClip, AnimationClip OnClip, string Name)
            {
                Tree = Master.Master.CreateBlendTreeChild(1);
                Tree.name = Name;
                
                Master.ReLinkChildren();

                Tree.blendType = BlendTreeType.Simple1D;
                Tree.blendParameter = parameter.name;
                Tree.AddChild(OffClip, 0f); // Add Motion
                Tree.AddChild(OnClip, 1f); // Add Motion
            }
        }

        public class MasterTree
        {
            public MasterTree(AnimatorController controller, AnimatorState State, string Name)
            {
                Master = CreateBlendTree(controller, State, Name);
                Master.blendType = BlendTreeType.Direct;

                DummyParameter = new AnimatorControllerParameter
                {
                    name = $"{Name}_DummyFloat",
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 1f
                };

                var parameters = controller.parameters.ToList();
                parameters.Add(DummyParameter);
                controller.parameters = parameters.ToArray();
            }

            public void ReLinkChildren()
            {
                // Re-Link All Child Trees To Master
                var ChildTrees = Master.children;
                for (var i = 0; i < ChildTrees.Length; i++)
                {
                    ChildTrees[i].directBlendParameter = DummyParameter.name;
                }
                Master.children = ChildTrees;
            }

            public BlendTree Master;

            public AnimatorControllerParameter DummyParameter;

            public List<BlendState> Trees = new List<BlendState>();
        }

        public AnimatorController controller;
        public AnimatorControllerLayer layer;
        public MasterTree masterTree;

        public DBT_Instance(AnimatorController controller, string LayerName)
        {
            var controllerPath = AssetDatabase.GetAssetPath(controller);

            var layer = new AnimatorControllerLayer
            {
                name = LayerName,
                defaultWeight = 1,
                stateMachine = new AnimatorStateMachine
                {
                    name = LayerName + " State Machine"
                },
            };

            controller.AddLayer(layer);

            AssetDatabase.AddObjectToAsset(layer.stateMachine, controllerPath);
            AssetDatabase.SaveAssets();

            this.layer = layer;

            this.controller = controller;

            var state = CreateState(layer, $"{LayerName}_BlendRootState");

            masterTree = new MasterTree(controller, state, $"{LayerName} Master Tree");
        }

        public static AnimatorState CreateState(AnimatorControllerLayer layer, string Name, Vector3? position = null)
        {
            var state = position == null ? layer.stateMachine.AddState(Name) : layer.stateMachine.AddState(Name, position.Value);
            state.writeDefaultValues = true;

            return state;
        }

        public static BlendTree CreateBlendTree(AnimatorController controller, AnimatorState State, string Name)
        {
            var tree = new BlendTree
            {
                name = Name,
                hideFlags = HideFlags.HideInHierarchy
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            State.motion = tree;

            return tree;
        }
    }
}
#endif