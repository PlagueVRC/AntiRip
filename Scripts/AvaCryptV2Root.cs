using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Collections.Generic;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using Thry;
using UnityEditor;
using UnityEditor.Animations;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
#endif

namespace GeoTetra.GTAvaCrypt
{
    public class AvaCryptV2Root : MonoBehaviour
    {
        [Header("Set high enough so your encrypted mesh is visuall. Default = .1")]
        [Range(.1f, .4f)]
        [SerializeField] 
        float _distortRatio = .2f;

        [Header("Ensure this is pointing to your LocalAvatarData folder!")]
        [SerializeField] 
        string _vrcSavedParamsPath = string.Empty;
        
        [Header("Materials in this list will also be locked and injected.")]
        [SerializeField] 
        List<Material> m_AdditionalMaterials = new List<Material>();

        [Header("Materials in this list will be ignored.")]
        [SerializeField] 
        List<Material> m_IgnoredMaterials = new List<Material>();
        
        [SerializeField] 
        public bool[] _bitKeys = new bool[32];
        
        StringBuilder _sb = new StringBuilder();

#if UNITY_EDITOR
        readonly AvaCryptController _avaCryptController = new AvaCryptController();

        public void ValidateAnimatorController()
        {
            var controller = GetAnimatorController();

            _avaCryptController.InitializeCount(_bitKeys.Length);
            _avaCryptController.ValidateLayers(controller);
            _avaCryptController.ValidateAnimations(gameObject, controller);
            _avaCryptController.ValidateParameters(controller);
            _avaCryptController.ValidateBitKeySwitches(controller);
        }

        AnimatorController GetAnimatorController()
        {
            if (transform.parent != null)
            {
                EditorUtility.DisplayDialog("AvaCryptRoot component not on a Root GameObject.", 
                    "The GameObject which the AvaCryptRoot component is placed on must not be the child of any other GameObject.", 
                    "Ok");
                return null;
            }
            
            var animator = GetComponent<Animator>();
            if (animator == null)
            {
                EditorUtility.DisplayDialog("No Animator.", 
                    "Add an animator to the Avatar's root GameObject.", 
                    "Ok");
                return null;
            }
            
            var runtimeController = animator.runtimeAnimatorController;
            if(runtimeController == null)
            {
                EditorUtility.DisplayDialog("Animator has no AnimatorController.", 
                    "Add an AnimatorController to the Animator component.", 
                    "Ok");
                return null;
            }
     
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(runtimeController));
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Could not get AnimatorController.", 
                    "This shouldn't happen... don't know why this would happen.", 
                    "Ok");
                return null;
            }

            return controller;
        }
        
        public void EncryptAvatar()
        {
            ValidateAnimatorController();
            
            var newName = gameObject.name + "_Encrypted";
            
            // delete old GO, do as such in case its disabled
            var scene = SceneManager.GetActiveScene();
            var sceneRoots = scene.GetRootGameObjects();
            foreach(var oldGameObject in sceneRoots)
            {
                if (oldGameObject.name == newName) DestroyImmediate(oldGameObject);
            }

            var encodedGameObject = Instantiate(gameObject);
            encodedGameObject.name = newName;
            encodedGameObject.SetActive(true);
            
            var data = new AvaCryptData(_bitKeys.Length);
            var decodeShader = AvaCryptMaterial.GenerateDecodeShader(data, _bitKeys);

            var meshFilters = encodedGameObject.GetComponentsInChildren<MeshFilter>(true);
            var skinnedMeshRenderers = encodedGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var aggregateIgnoredMaterials = new List<Material>();

            // Gather all materials to ignore based on if they are shared in mesh
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.GetComponent<MeshRenderer>() != null)
                {
                    var materials = meshFilter.GetComponent<MeshRenderer>().sharedMaterials;
                    AddMaterialsToIgnoreList(materials, aggregateIgnoredMaterials);
                }
            }
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                var materials = skinnedMeshRenderer.sharedMaterials;
                AddMaterialsToIgnoreList(materials, aggregateIgnoredMaterials);
            }
            
            EncryptMaterials(m_AdditionalMaterials.ToArray(), decodeShader, aggregateIgnoredMaterials);

            // Do encrypting
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.GetComponent<MeshRenderer>() != null)
                {
                    var materials = meshFilter.GetComponent<MeshRenderer>().sharedMaterials;
                    if (EncryptMaterials(materials, decodeShader, aggregateIgnoredMaterials))
                    {
                        meshFilter.sharedMesh = AvaCryptMesh.EncryptMesh(meshFilter.sharedMesh, _distortRatio, data);
                    }
                    else
                    {
                        Debug.Log($"Ignoring Encrypt on {meshFilter.gameObject} contains ignored material!");
                    }
                }
            }
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.GetComponent<Cloth>() == null)
                {
                    var materials = skinnedMeshRenderer.sharedMaterials;
                    if (EncryptMaterials(materials, decodeShader, aggregateIgnoredMaterials))
                    {
                        skinnedMeshRenderer.sharedMesh =
                            AvaCryptMesh.EncryptMesh(skinnedMeshRenderer.sharedMesh, _distortRatio, data);
                    }
                    else
                    {
                        Debug.Log($"Ignoring Encrypt on {skinnedMeshRenderer.gameObject} contains ignored material!");
                    }
                }
                else
                {
                    Debug.Log($"Ignoring Encrypt on {skinnedMeshRenderer.gameObject} is a cloth material!");
                }
            }

            var avaCryptRoots = encodedGameObject.GetComponentsInChildren<AvaCryptV2Root>();
            foreach (var avaCryptRoot in avaCryptRoots)
            {
                DestroyImmediate(avaCryptRoot);
            }
            
            // Disable old for convienence.
            gameObject.SetActive(false);
        }

        void AddMaterialsToIgnoreList(Material[] materials, List<Material> aggregateIgnoredMaterials)
        {
            foreach (var material in materials)
            {
                if (m_IgnoredMaterials.Contains(material))
                {
                    aggregateIgnoredMaterials.AddRange(materials);
                    return;
                }
            }
        }

        bool EncryptMaterials(Material[] materials, string decodeShader,  List<Material> aggregateIgnoredMaterials)
        {
            var materialEncrypted = false;
            var ignoredMats = false;
            foreach (var mat in materials)
            {
                if (mat != null && mat.shader.name.Contains(".poiyomi/Poiyomi") && mat.shader.name.Contains("Pro"))
                {
                    if (!mat.shader.name.Contains("Hidden/Locked"))
                    {
                        ShaderOptimizer.SetLockedForAllMaterials(new []{mat}, 1, true, false, false);
                    }
                    
                    if (!mat.shader.name.Contains("Hidden/Locked"))
                    {
                        Debug.LogError($"{mat.name} {mat.shader.name} Trying to Inject not-locked shader?!");
                        continue;
                    }

                    if (aggregateIgnoredMaterials.Contains(mat))
                    {
                        ignoredMats = true;
                        continue;
                    }

                    var shaderPath = AssetDatabase.GetAssetPath(mat.shader);
                    var path = Path.GetDirectoryName(shaderPath);
                    var decodeShaderPath = Path.Combine(path, "GTModelDecode.cginc");
;                   File.WriteAllText(decodeShaderPath, decodeShader);

                    var shaderText = File.ReadAllText(shaderPath);
                    if (!shaderText.Contains("//AvaCrypt Injected"))
                    {
                        _sb.Clear();
                        _sb.AppendLine("//AvaCrypt Injected");
                        _sb.Append(shaderText);
                        _sb.ReplaceOrLog(AvaCryptMaterial.DefaultPoiUV, AvaCryptMaterial.AlteredPoiUV);
                        // _sb.ReplaceOrLog(AvaCryptMaterial.DefaultPoiUVArray, AvaCryptMaterial.AlteredPoiUVArray);
                        if (!_sb.ReplaceOrLog(AvaCryptMaterial.DefaultPoiVert, AvaCryptMaterial.AlteredPoiVert))
                        {
                            _sb.ReplaceOrLog(AvaCryptMaterial.NewDefaultPoiVert, AvaCryptMaterial.NewAlteredPoiVert);
                        }
                        _sb.ReplaceOrLog(AvaCryptMaterial.DefaultVertSetup, AvaCryptMaterial.AlteredVertSetup);
                        // _sb.ReplaceOrLog(AvaCryptMaterial.DefaultUvTransfer, AvaCryptMaterial.AlteredUvTransfer);
                        _sb.ReplaceOrLog(AvaCryptMaterial.DefaultFallback, AvaCryptMaterial.AlteredFallback);
                        File.WriteAllText(shaderPath, _sb.ToString());
                    }

                    foreach (var include in Directory.GetFiles(path, "*.cginc"))
                    {
                        var includeText = File.ReadAllText(include);
                        if (!includeText.Contains("//AvaCrypt Injected"))
                        {
                            _sb.Clear();
                            _sb.AppendLine("//AvaCrypt Injected");
                            _sb.Append(includeText);
                            _sb.ReplaceOrLog(AvaCryptMaterial.DefaultPoiUV, AvaCryptMaterial.AlteredPoiUV);
                            // _sb.ReplaceOrLog(AvaCryptMaterial.DefaultPoiUVArray, AvaCryptMaterial.AlteredPoiUVArray);
                            if (!_sb.ReplaceOrLog(AvaCryptMaterial.DefaultPoiVert, AvaCryptMaterial.AlteredPoiVert))
                            {
                                _sb.ReplaceOrLog(AvaCryptMaterial.NewDefaultPoiVert, AvaCryptMaterial.NewAlteredPoiVert);
                            }
                            _sb.ReplaceOrLog(AvaCryptMaterial.DefaultVertSetup, AvaCryptMaterial.AlteredVertSetup);
                            // _sb.ReplaceOrLog(AvaCryptMaterial.DefaultUvTransfer, AvaCryptMaterial.AlteredUvTransfer);
                            _sb.ReplaceOrLog(AvaCryptMaterial.DefaultFallback, AvaCryptMaterial.AlteredFallback);
                            File.WriteAllText(include, _sb.ToString());
                        }
                    }

                    materialEncrypted = true;
                }
            }

            return materialEncrypted && !ignoredMats;
        }

        public void WriteBitKeysToExpressions()
        {
#if VRC_SDK_VRCSDK3
            var descriptor = GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
            {
                Debug.LogError("Keys not written! Couldn't find VRCAvatarDescriptor next to GTAvaCryptRoot");
                EditorUtility.DisplayDialog("Keys not written! Missing PipelineManager!", "Put AvaCryptRoot next to VRCAvatarDescriptor and run Write Keys again.", "Okay");
                return;
            }

            if (descriptor.expressionParameters == null)
            {
                Debug.LogError("Keys not written! Expressions is not filled in on VRCAvatarDescriptor!");
                EditorUtility.DisplayDialog("Keys not written! Expressions is not filled in on VRCAvatarDescriptor!", "Fill in the Parameters slot on the VRCAvatarDescriptor and run again.", "Okay");
                return;
            }

            if (AddBitKeys(descriptor.expressionParameters))
            {
                WriteKeysToSaveFile();
            }

#else
            Debug.LogError("Can't find VRC SDK?");
            EditorUtility.DisplayDialog("Can't find VRC SDK?", "You need to isntall VRC SDK.", "Okay");
#endif
        }

        public void WriteKeysToSaveFile()
        {
#if VRC_SDK_VRCSDK3
            var pipelineManager = GetComponent<PipelineManager>();
            if (pipelineManager == null)
            {
                Debug.LogError("Keys not written! Couldn't find PipelineManager next to GTAvaCryptRoot");
                EditorUtility.DisplayDialog("Keys not written! Couldn't find PipelineManager next to GTAvaCryptRoot", "Put AvaCryptRoot next to PipelineManager and run Write Keys again.", "Okay");
                return;
            }

            if (string.IsNullOrWhiteSpace(pipelineManager.blueprintId))
            {
                Debug.LogError("Blueprint ID not filled in!");
                EditorUtility.DisplayDialog("Keys not written! Blueprint ID not filled in!", "You need to first populate your PipelineManager with a Blueprint ID before keys can be written. Publish your avatar to get the Blueprint ID, attach the ID through the PipelineManager then run Write Keys again.","Okay");
                return;
            }

            if (!Directory.Exists(_vrcSavedParamsPath))
            {
                Debug.LogError("Keys not written! Could not find VRC LocalAvatarData folder!");
                EditorUtility.DisplayDialog("Could not find VRC LocalAvatarData folder!", "Ensure the VRC Saved Params Path is point to your LocalAvatarData folder, should be at C:\\Users\\username\\AppData\\LocalLow\\VRChat\\VRChat\\LocalAvatarData\\, then run Write Keys again.","Okay");
                return;
            }

            foreach (var userDir in Directory.GetDirectories(_vrcSavedParamsPath))
            {
                var filePath = $"{userDir}\\{pipelineManager.blueprintId}";
                Debug.Log($"Writing keys to {filePath}");
                ParamFile paramFile = null;
                if (File.Exists(filePath))
                {
                    Debug.Log($"Avatar param file already exists, loading and editing.");
                    var json = File.ReadAllText(filePath);
                    paramFile = JsonUtility.FromJson<ParamFile>(json);
                }

                if (paramFile == null)
                {
                    paramFile = new ParamFile();
                    paramFile.animationParameters = new List<ParamFileEntry>();
                }

                for (var i = 0; i < _bitKeys.Length; ++i)
                {
                    var entryIndex = paramFile.animationParameters.FindIndex(p => p.name == $"BitKey{i}");
                    if (entryIndex != -1)
                    {
                        paramFile.animationParameters[entryIndex].value = _bitKeys[i] ? 1 : 0;
                    }
                    else
                    {
                        var newEntry = new ParamFileEntry()
                        {
                            name = $"BitKey{i}",
                            value = _bitKeys[i] ? 1 : 0
                        };
                        paramFile.animationParameters.Add(newEntry);
                    }
                }
                
                System.IO.File.WriteAllText(filePath, JsonUtility.ToJson(paramFile));
            }
            
            EditorUtility.DisplayDialog("Successfully Wrote Keys!", "Your avatar should now just work in VRChat. If you accidentally hit 'Reset Avatar' in VRC 3.0 menu, you need to run this again.","Okay");
            
#else
            Debug.LogError("Can't find VRC SDK?");
            EditorUtility.DisplayDialog("Can't find VRC SDK?", "You need to isntall VRC SDK.", "Okay");
#endif
        }

        [Serializable]
        public class ParamFile
        {
            public List<ParamFileEntry> animationParameters;
        }
        
        [Serializable]
        public class ParamFileEntry
        {
            public string name;
            public float value;
        }

        void Reset()
        {
            GenerateNewKey();
            _vrcSavedParamsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\AppData\\LocalLow\\VRChat\\VRChat\\LocalAvatarData\\";
        }


        [ContextMenu("CleanupBlendTrees")]
        public void GenerateNewKey()
        {
            for (var i = 0; i < _bitKeys.Length; ++i)
            {
                _bitKeys[i] = Random.Range(-1f, 1f) > 0;
            }
        }
        
#if VRC_SDK_VRCSDK3
        [MenuItem("CONTEXT/VRCExpressionParameters/Add BitKeys")]
        void AddBitKeys(MenuCommand command)
        {
            var parameters = (VRCExpressionParameters) command.context;
            AddBitKeys(parameters);
        }

        public bool AddBitKeys(VRCExpressionParameters parameters)
        {
            var paramList = parameters.parameters.ToList();
            
            for (var i = 0; i < _bitKeys.Length; ++i)
            {
                var bitKeyName = $"BitKey{i}";
                
                var index = Array.FindIndex(parameters.parameters, p => p.name == bitKeyName);
                if (index != -1)
                {
                    Debug.Log($"Found BitKey in params {bitKeyName}");
                    parameters.parameters[index].saved = true;
                    parameters.parameters[index].defaultValue = 0;
                    parameters.parameters[index].valueType = VRCExpressionParameters.ValueType.Bool;
                }
                else
                {
                    Debug.Log($"Adding BitKey in params {bitKeyName}");
                    var newParam = new VRCExpressionParameters.Parameter
                    {
                        name = bitKeyName,
                        saved = true,
                        defaultValue = 0,
                        valueType = VRCExpressionParameters.ValueType.Bool
                    };
                    paramList.Add(newParam);
                }
            }
            
            parameters.parameters = paramList.ToArray();
            
            var remainingCost = VRCExpressionParameters.MAX_PARAMETER_COST - parameters.CalcTotalCost();;
            Debug.Log(remainingCost);
            if (remainingCost < 0)
            {
                Debug.LogError("Adding BitKeys took up too many parameters!");
                EditorUtility.DisplayDialog("Adding BitKeys took up too many parameters!", "Go to your VRCExpressionParameters and remove some unnecessary parameters to make room for the 32 BitKey bools and run this again.", "Okay");
                return false;
            }
            
            EditorUtility.SetDirty(parameters);

            return true;
        }
        
        [MenuItem("CONTEXT/VRCExpressionParameters/Remove BitKeys")]
        static void RemoveBitKeys(MenuCommand command)
        {
            var parameters = (VRCExpressionParameters) command.context;
            RemoveBitKeys(parameters);
        }
        
        public static void RemoveBitKeys(VRCExpressionParameters parameters)
        {
            var parametersList = parameters.parameters.ToList();
            parametersList.RemoveAll(p => p.name.Contains("BitKey"));
            parameters.parameters = parametersList.ToArray();
            
            EditorUtility.SetDirty(parameters);
        }
#endif

        [ContextMenu("Delete AvaCrypt Objects From Controller")]
        public void DeleteAvaCryptObjectsFromController()
        {
            _avaCryptController.InitializeCount(_bitKeys.Length);
            _avaCryptController.DeleteAvaCryptObjectsFromController(GetAnimatorController());
        }
#endif
    }
    public static class GTExtensions
    {
        public static bool ReplaceOrLog(this StringBuilder text, string textToReplace, string replaceWith)
        {
            if (text.IndexOf(textToReplace) != -1)
            {
                text.Replace(textToReplace, replaceWith);

                return true;
            }
            else
            {
                Debug.LogError($"{text} Does Not Contain {textToReplace}!");
            }

            return false;
        }
    }

    // Credit: Poiyomi & Thry - Embedded due to the want of no external dependencies.
    public static class ShaderStringBuilderExtensions
    {
        public static StringBuilder Prepend(this StringBuilder builder, string value) => builder.Insert(0, value);

        public static StringBuilder PrependLine(this StringBuilder builder, string value) => builder.Prepend(Environment.NewLine).Prepend(value);

        public static StringBuilder AppendLineTabbed(this StringBuilder builder, int tabLevel, string value)
        {
            return builder.Append(Tabs(tabLevel)).AppendLine(value);
        }

        public static StringBuilder PrependLineTabbed(this StringBuilder builder, int tabLevel, string value)
        {
            return builder.PrependLine(value).Prepend(Tabs(tabLevel));
        }

        public static StringBuilder AppendTabbed(this StringBuilder builder, int tabLevel, string value)
        {
            return builder.Append(Tabs(tabLevel)).Append(value);
        }

        public static StringBuilder PrependTabbed(this StringBuilder builder, int tabLevel, string value)
        {
            return builder.Prepend(value).Prepend(Tabs(tabLevel));
        }

        public static StringBuilder AppendMultilineTabbed(this StringBuilder builder, int tabLevel, string value)
        {
            var sr = new StringReader(value);
            string line;
            while ((line = sr.ReadLine()) != null)
                builder.AppendLineTabbed(tabLevel, line);
            return builder;
        }

        static string Tabs(int n)
        {
            if (n < 0)
                n = 0;
            return new string('\t', n);
        }

        public static bool Contains(this StringBuilder haystack, string needle)
        {
            return haystack.IndexOf(needle) != -1;
        }

        public static int IndexOf(this StringBuilder haystack, string needle)
        {
            if (haystack == null || needle == null)
                throw new ArgumentNullException();
            if (needle.Length == 0)
                return 0;//empty strings are everywhere!
            if (needle.Length == 1)//can't beat just spinning through for it
            {
                var c = needle[0];
                for (var idx = 0; idx != haystack.Length; ++idx)
                    if (haystack[idx] == c)
                        return idx;
                return -1;
            }
            var m = 0;
            var i = 0;
            var T = KmpTable(needle);
            while (m + i < haystack.Length)
            {
                if (needle[i] == haystack[m + i])
                {
                    if (i == needle.Length - 1)
                        return m == needle.Length ? -1 : m;//match -1 = failure to find conventional in .NET
                    ++i;
                }
                else
                {
                    m = m + i - T[i];
                    i = T[i] > -1 ? T[i] : 0;
                }
            }
            return -1;
        }
        private static int[] KmpTable(string sought)
        {
            var table = new int[sought.Length];
            var pos = 2;
            var cnd = 0;
            table[0] = -1;
            table[1] = 0;
            while (pos < table.Length)
                if (sought[pos - 1] == sought[cnd])
                    table[pos++] = ++cnd;
                else if (cnd > 0)
                    cnd = table[cnd];
                else
                    table[pos++] = 0;
            return table;
        }
    }
}
