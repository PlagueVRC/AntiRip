using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using System.Collections;
using Object = UnityEngine.Object;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using Thry;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
#endif

namespace Kanna.Protecc
{
    public class KannaProteccRoot : MonoBehaviour
    {
        public static KannaProteccRoot Instance;

#if UNITY_EDITOR
        public Obfuscator obfuscator = new Obfuscator();
#endif

        [SerializeField]
        public bool IsProtected;

        [Header("Set high enough so your encrypted mesh is visuall. Default = .1")]
        [Range(.1f, .4f)]
        [SerializeField] 
        float _distortRatio = .4f;

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

        public readonly string pathPrefix = "Assets/Kanna/Obfuscated Files/";

        [SerializeField]
        public string path = "";

        [SerializeField]
        public bool disableObjectNameObfuscation = false;

        [SerializeField]
        public List<Object> excludeObjectNames = new List<Object>();

        [SerializeField]
        public List<string> excludeParamNames = new List<string>();

        [SerializeField]
        public StringStringSerializableDictionary ParameterRenamedValues = new StringStringSerializableDictionary();

        public string GetBitKeyName(int id, int LimitRenameLength = -1)
        {
            return ParameterRenamedValues.Any(o => o.Key == $"BitKey{id}") ? (LimitRenameLength == -1 ? ParameterRenamedValues.First(o => o.Key == $"BitKey{id}").Value : ParameterRenamedValues.First(o => o.Key == $"BitKey{id}").Value.Substring(0, LimitRenameLength)) : $"BitKey{id}";
        }

        StringBuilder _sb = new StringBuilder();

#if UNITY_EDITOR
        readonly KannaProteccController _KannaProteccController = new KannaProteccController();

        public void ValidateAnimatorController(GameObject obj, AnimatorController controller)
        {
            _KannaProteccController.InitializeCount(_bitKeys.Length);
            _KannaProteccController.ValidateAnimations(obj, controller);
            _KannaProteccController.ValidateParameters(controller);
            _KannaProteccController.ValidateLayers(controller);

            obfuscator.ObfuscateLayer(controller.layers.First(o => o.name == KannaProteccController.LayerName), controller, this);
        }

        AnimatorController GetAnimatorController()
        {
            if (transform.parent != null)
            {
                EditorUtility.DisplayDialog("KannaProteccRoot component not on a Root GameObject.", 
                    "The GameObject which the KannaProteccRoot component is placed on must not be the child of any other GameObject.", 
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
            
            var data = new KannaProteccData(_bitKeys.Length);
            var decodeShader = KannaProteccMaterial.GenerateDecodeShader(data, _bitKeys);

            var meshFilters = encodedGameObject.GetComponentsInChildren<MeshFilter>(true).Select(o => (o, o.gameObject.activeSelf));
            var skinnedMeshRenderers = encodedGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(o => (o, o.gameObject.activeSelf));

            var aggregateIgnoredMaterials = new List<Material>();

            // Gather all materials to ignore based on if they are shared in mesh
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.o.GetComponent<MeshRenderer>() != null)
                {
                    var materials = meshFilter.o.GetComponent<MeshRenderer>().sharedMaterials;
                    AddMaterialsToIgnoreList(materials, aggregateIgnoredMaterials);
                }
                else
                {
                    Debug.LogError("WTF? -> " + meshFilter.o.name);
                }
            }

            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                var materials = skinnedMeshRenderer.o.sharedMaterials;
                AddMaterialsToIgnoreList(materials, aggregateIgnoredMaterials);
            }
            
            EncryptMaterials(m_AdditionalMaterials.ToArray(), decodeShader, aggregateIgnoredMaterials);

            // Do encrypting
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.o.GetComponent<MeshRenderer>() != null)
                {
                    meshFilter.o.gameObject.SetActive(true);
                    var materials = meshFilter.o.GetComponent<MeshRenderer>().sharedMaterials;
                    if (EncryptMaterials(materials, decodeShader, aggregateIgnoredMaterials))
                    {
                        meshFilter.o.sharedMesh = KannaProteccMesh.EncryptMesh(meshFilter.o.sharedMesh, _distortRatio, data);
                    }
                    else
                    {
                        Debug.Log($"Ignoring Encrypt on {meshFilter.o.gameObject.name} contains ignored material!");
                    }

                    meshFilter.o.gameObject.SetActive(meshFilter.activeSelf);
                }
                else
                {
                    Debug.LogError("WTF? -> " + meshFilter.o.name);
                }
            }

            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.o.GetComponent<Cloth>() == null)
                {
                    skinnedMeshRenderer.o.gameObject.SetActive(true);
                    var materials = skinnedMeshRenderer.o.sharedMaterials;
                    if (EncryptMaterials(materials, decodeShader, aggregateIgnoredMaterials))
                    {
                        skinnedMeshRenderer.o.sharedMesh = KannaProteccMesh.EncryptMesh(skinnedMeshRenderer.o.sharedMesh, _distortRatio, data);
                    }
                    else
                    {
                        Debug.Log($"Ignoring Encrypt on {skinnedMeshRenderer.o.gameObject.name} contains ignored material!");
                    }

                    skinnedMeshRenderer.o.gameObject.SetActive(skinnedMeshRenderer.activeSelf);
                }
                else
                {
                    Debug.Log($"Ignoring Encrypt on {skinnedMeshRenderer.o.gameObject.name} is a cloth material!");
                }
            }

            var KannaProteccRoots = encodedGameObject.GetComponentsInChildren<KannaProteccRoot>(true);
            foreach (var KannaProteccRoot in KannaProteccRoots)
            {
                DestroyImmediate(KannaProteccRoot);
            }
            
            // Disable old for convienence.
            gameObject.SetActive(false);

            // Force unity to import things
            AssetDatabase.Refresh();

            // Do Obfuscation
            var newobj = obfuscator.Obfuscate(encodedGameObject, this);

            encodedGameObject.SetActive(false); // Temp

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            // Force unity to import things
            AssetDatabase.Refresh();

            WriteBitKeysToExpressions(newobj.GetComponent<VRCAvatarDescriptor>().expressionParameters, true);

            ValidateAnimatorController(newobj, AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(newobj.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers.First(o => o.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController)));

            IsProtected = true;

            EditorUtility.DisplayDialog("Successfully Encrypted!", "Keys were automatically written. Your avatar should be ready to upload!", "Okay");
        }

        void AddMaterialsToIgnoreList(Material[] materials, List<Material> aggregateIgnoredMaterials)
        {
            foreach (var material in materials)
            {
                if (m_IgnoredMaterials.Any(o => o.name == material.name && o.ComputeCRC() == material.ComputeCRC()))
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
                if (mat != null && KannaProteccMaterial.IsShaderSupported(mat.shader, out var shaderMatch))
                {
                    if (shaderMatch.SupportsLocking && !mat.shader.name.Contains("Locked"))
                    {
                        ShaderOptimizer.SetLockedForAllMaterials(new []{mat}, 1, true, false, false);
                    }
                    
                    if (shaderMatch.SupportsLocking && !mat.shader.name.Contains("Locked"))
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
                    var decodeShaderPath = Path.Combine(path, "KannaModelDecode.cginc");
                    File.WriteAllText(decodeShaderPath, decodeShader);

                    var shaderText = File.ReadAllText(shaderPath);
                    if (!shaderText.Contains("//KannaProtecc Injected"))
                    {
                        _sb.Clear();
                        _sb.AppendLine("//KannaProtecc Injected");
                        _sb.Append(shaderText);

                        _sb.ReplaceOrLog(shaderMatch.UV.TextToFind, shaderMatch.UV.TextToReplaceWith);

                        _sb.ReplaceOrLog(shaderMatch.Vert.TextToFind, shaderMatch.Vert.TextToReplaceWith);

                        _sb.ReplaceOrLog(shaderMatch.VertexSetup.TextToFind, shaderMatch.VertexSetup.TextToReplaceWith);

                        File.WriteAllText(shaderPath.Replace(Path.GetExtension(shaderPath), "") + "_Protected.shader", _sb.ToString());

                        AssetDatabase.Refresh();

                        mat.shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath.Replace(Path.GetExtension(shaderPath), "") + "_Protected.shader");
                        shaderPath = shaderPath.Replace(Path.GetExtension(shaderPath), "") + "_Protected.shader";

                        mat.SetOverrideTag("VRCFallback", "Hidden");
                    }

                    var IncludeFileDirs = new List<string>();

                    foreach (var include in mat.shader.FindAllShaderIncludes().Where(o => !o.Contains("KannaModelDecode.cginc")))
                    {
                        var includeText = File.ReadAllText(include);

                        if (!includeText.Contains("//KannaProtecc Injected"))
                        {
                            _sb.Clear();
                            _sb.AppendLine("//KannaProtecc Injected\r\n");
                            _sb.Append(includeText);

                            if (shaderMatch.UV.ApplyToIncludes && shaderMatch.UV.ExcludeIncludes.All(o => !include.Contains(o)))
                                _sb.ReplaceOrLog(shaderMatch.UV.TextToFind, shaderMatch.UV.TextToReplaceWith);

                            if (shaderMatch.Vert.ApplyToIncludes && shaderMatch.Vert.ExcludeIncludes.All(o => !include.Contains(o)))
                                _sb.ReplaceOrLog(shaderMatch.Vert.TextToFind, shaderMatch.Vert.TextToReplaceWith);

                            if (shaderMatch.VertexSetup.ApplyToIncludes && shaderMatch.VertexSetup.ExcludeIncludes.All(o => !include.Contains(o)))
                                _sb.ReplaceOrLog(shaderMatch.VertexSetup.TextToFind, shaderMatch.VertexSetup.TextToReplaceWith);

                            var newFileName = include.Replace(Path.GetExtension(include), "") + $"_Protected{Path.GetExtension(include)}";
                            IncludeFileDirs.Add(newFileName);
                            File.WriteAllText(newFileName, _sb.ToString());
                        }
                    }

                    var FileText = File.ReadAllText(shaderPath);

                    foreach (var dir in IncludeFileDirs)
                    {
                        var newName = Path.GetFileName(dir);

                        FileText = FileText.Replace(newName.Replace("_Protected", ""), newName);
                    }

                    foreach (var dir in IncludeFileDirs) // get each file to write to
                    {
                        foreach (var towrite in IncludeFileDirs) // write all new include names
                        {
                            var newName2 = Path.GetFileName(towrite);

                            File.WriteAllText(dir, File.ReadAllText(dir).Replace(newName2.Replace("_Protected", ""), newName2));
                        }
                    }

                    File.WriteAllText(shaderPath, FileText);

                    materialEncrypted = true;
                }
            }

            return materialEncrypted && !ignoredMats;
        }

        public void WriteBitKeysToExpressions(VRCExpressionParameters ExtraParams = null, bool WriteOnlyToExtra = false, bool DoLog = false)
        {
#if VRC_SDK_VRCSDK3
            var descriptor = GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
            {
                Debug.LogError("Keys not written! Couldn't find VRCAvatarDescriptor next to KannaProteccRoot");
                EditorUtility.DisplayDialog("Keys not written! Missing PipelineManager!", "Put KannaProteccRoot next to VRCAvatarDescriptor and run Write Keys again.", "Okay");
                return;
            }

            if (string.IsNullOrEmpty(GetComponent<PipelineManager>()?.blueprintId))
            {
                return;
            }

            if (descriptor.expressionParameters == null)
            {
                Debug.LogError("Keys not written! Expressions is not filled in on VRCAvatarDescriptor!");
                EditorUtility.DisplayDialog("Keys not written! Expressions is not filled in on VRCAvatarDescriptor!", "Fill in the Parameters slot on the VRCAvatarDescriptor and run again.", "Okay");
                return;
            }

            if ((WriteOnlyToExtra || AddBitKeys(descriptor.expressionParameters, this)) && (ExtraParams == null || AddBitKeys(ExtraParams, this, false)))
            {
                WriteKeysToSaveFile(DoLog);
            }

#else
            Debug.LogError("Can't find VRC SDK?");
            EditorUtility.DisplayDialog("Can't find VRC SDK?", "You need to isntall VRC SDK.", "Okay");
#endif
        }

        public void WriteKeysToSaveFile(bool DoLog = false)
        {
#if VRC_SDK_VRCSDK3
            var pipelineManager = GetComponent<PipelineManager>();
            if (pipelineManager == null)
            {
                Debug.LogError("Keys not written! Couldn't find PipelineManager next to KannaProteccRoot");
                EditorUtility.DisplayDialog("Keys not written! Couldn't find PipelineManager next to KannaProteccRoot", "Put KannaProteccRoot next to PipelineManager and run Write Keys again.", "Okay");
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

                    if (paramFile.animationParameters.Any(o => o.name.Length == 32 && o.name.All(a => !char.IsUpper(a))))
                    {
                        paramFile.animationParameters.Clear(); // Has Obfuscated Params, So We Can't Tell. Let's Not Make Chonky Params Files.
                    }
                }

                if (paramFile == null)
                {
                    paramFile = new ParamFile();
                    paramFile.animationParameters = new List<ParamFileEntry>();
                }

                for (var i = 0; i < _bitKeys.Length; ++i)
                {
                    var entryIndex = paramFile.animationParameters.FindIndex(p => p.name == GetBitKeyName(i));
                    if (entryIndex != -1)
                    {
                        paramFile.animationParameters[entryIndex].value = _bitKeys[i] ? 1 : 0;
                    }
                    else
                    {
                        var newEntry = new ParamFileEntry()
                        {
                            name = GetBitKeyName(i),
                            value = _bitKeys[i] ? 1 : 0
                        };
                        paramFile.animationParameters.Add(newEntry);
                    }
                }
                
                File.WriteAllText(filePath, JsonUtility.ToJson(paramFile));
            }
            
            if (DoLog)
                EditorUtility.DisplayDialog("Successfully Wrote Keys!", "Your avatar should now just work in VRChat. If you accidentally hit 'Reset Avatar' in VRC 3.0 menu, you need to click Write Keys again.","Okay");
            
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
        
        [MenuItem("CONTEXT/VRCExpressionParameters/Add BitKeys")]
        static void AddBitKeys(MenuCommand command)
        {
            var parameters = (VRCExpressionParameters) command.context;
            AddBitKeys(parameters, Instance);
        }

        public static bool AddBitKeys(VRCExpressionParameters parameters, KannaProteccRoot root, bool RemoveOld = true)
        {
            if (RemoveOld)
            {
                //RemoveBitKeys(parameters);
            }

            var paramList = parameters.parameters.ToList();
            
            for (var i = 0; i < root._bitKeys.Length; ++i)
            {
                var bitKeyName = root.GetBitKeyName(i);
                
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
            
            var remainingCost = VRCExpressionParameters.MAX_PARAMETER_COST - parameters.CalcTotalCost();
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
        
        //[MenuItem("CONTEXT/VRCExpressionParameters/Remove BitKeys")]
        //static void RemoveBitKeys(MenuCommand command)
        //{
        //    var parameters = (VRCExpressionParameters) command.context;
        //    RemoveBitKeys(parameters);
        //}
        
        //public static void RemoveBitKeys(VRCExpressionParameters parameters)
        //{
        //    var parametersList = parameters.parameters.ToList();
        //    parametersList.RemoveAll(p => p.name.Contains("BitKey") || (p.valueType == VRCExpressionParameters.ValueType.Bool && p.name.Length == 32 && p.name.All(a => !char.IsUpper(a))));
        //    parameters.parameters = parametersList.ToArray();
            
        //    EditorUtility.SetDirty(parameters);
        //}

        [ContextMenu("Delete KannaProtecc Objects From Controller")]
        public void DeleteKannaProteccObjectsFromController()
        {
            _KannaProteccController.InitializeCount(_bitKeys.Length);
            _KannaProteccController.DeleteKannaProteccObjectsFromController(GetAnimatorController());
        }
#endif
    }
    public static class KannaExtensions
    {
        public static bool ReplaceOrLog(this StringBuilder text, string[] textToReplace, string replaceWith)
        {
            var AnyFound = false;

            foreach (var tofind in textToReplace)
            {
                if (text.IndexOf(tofind) != -1)
                {
                    text.Replace(tofind, replaceWith.Replace("{OrigText}", tofind));
                    AnyFound = true;
                }
                else
                {
                    //Debug.LogError($"{text} Does Not Contain {textToReplace}!");
                }
            }

            return AnyFound;
        }

        public static string GetRelativePath(this string RelativeDir, string toPath)
        {
            if (toPath[0] == '/' || toPath[0] == '\\')
            {
                toPath = toPath.Substring(1);
            }

            var combinedPath = Path.Combine(RelativeDir, toPath); // C:/shaders/myshader.shader/../somefile.cginc
            var absolutePath = Path.GetFullPath(combinedPath); // C:/shaders/somefile.cginc

            Debug.Log(absolutePath);

            return absolutePath;
        }

        public static List<string> FindAllShaderIncludes(this Shader shader)
        {
            var FoundIncludes = new List<string>();
            
            var ShaderText = File.ReadAllText(AssetDatabase.GetAssetPath(shader));

            FindIncludes(ShaderText, Path.GetDirectoryName(AssetDatabase.GetAssetPath(shader)));

            void FindIncludes(string searchMe, string dir)
            {
                if (searchMe.IndexOf("#include") > -1)
                {
                    foreach (var line in searchMe.Split(new [] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.IndexOf("#include") > -1)
                        {
                            var StartIndex = line.IndexOf("#include \"") + "#include \"".Length;
                            var EndIndex = line.LastIndexOf("\"");

                            var IncludeName = dir.GetRelativePath(line.Substring(StartIndex, EndIndex - StartIndex));

                            if (File.Exists(IncludeName) && !FoundIncludes.Contains(IncludeName))
                            {
                                FoundIncludes.Add(IncludeName);
                                FindIncludes(File.ReadAllText(IncludeName), Path.GetDirectoryName(IncludeName));
                            }
                        }
                    }
                }
            }

            return FoundIncludes;
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

[Serializable]
public class StringStringSerializableDictionary : SerializableDictionary<string, string>
{ }

[Serializable]
public class SerializableDictionary<TKey, TValue> : IDictionary<TKey, TValue>//, ISerializationCallbackReceiver
{
    [SerializeField] private List<TKey> keys = new List<TKey>();
    [SerializeField] private List<TValue> values = new List<TValue>();

    // implement the IDictionary interface methods using the lists
    public TValue this[TKey key]
    {
        get
        {
            var index = keys.IndexOf(key);
            if (index < 0) throw new KeyNotFoundException();
            return values[index];
        }
        set
        {
            var index = keys.IndexOf(key);
            if (index < 0)
            {
                keys.Add(key);
                values.Add(value);
            }
            else
            {
                values[index] = value;
            }
        }
    }

    public ICollection<TKey> Keys => keys;

    public ICollection<TValue> Values => values;

    public int Count => keys.Count;

    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
    {
        if (ContainsKey(key)) throw new ArgumentException();
        keys.Add(key);
        values.Add(value);
    }

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    public void Clear()
    {
        keys.Clear();
        values.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        var index = keys.IndexOf(item.Key);
        if (index < 0) return false;
        return EqualityComparer<TValue>.Default.Equals(values[index], item.Value);
    }

    public bool ContainsKey(TKey key)
    {
        return keys.Contains(key);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        for (var i = 0; i < Count; i++)
        {
            array[arrayIndex + i] = new KeyValuePair<TKey, TValue>(keys[i], values[i]);
        }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return new KeyValuePair<TKey, TValue>(keys[i], values[i]);
        }
    }

    public bool Remove(TKey key)
    {
        var index = keys.IndexOf(key);
        if (index < 0) return false;
        keys.RemoveAt(index);
        values.RemoveAt(index);
        return true;
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        var index = keys.IndexOf(item.Key);
        if (index < 0) return false;
        if (!EqualityComparer<TValue>.Default.Equals(values[index], item.Value)) return false;
        keys.RemoveAt(index);
        values.RemoveAt(index);
        return true;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        var index = keys.IndexOf(key);
        if (index < 0)
        {
            value = default;
            return false;
        }
        value = values[index];
        return true;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    //void ISerializationCallbackReceiver.OnBeforeSerialize()
    //{
    //    keys.Clear();
    //    values.Clear();

    //    foreach (KeyValuePair<TKey, TValue> pair in this)
    //    {
    //        keys.Add(pair.Key);
    //        values.Add(pair.Value);
    //    }
    //}

    //void ISerializationCallbackReceiver.OnAfterDeserialize()
    //{
    //    this.Clear();

    //    if (keys.Count != values.Count)
    //    {
    //        throw new System.Exception(string.Format($"Error after deserialization in SerializableDictionary class. There are {keys.Count} keys and {values.Count} values after deserialization. Could not load SerializableDictionary"));
    //    }

    //    for (int i = 0; i < keys.Count; i++)
    //    {
    //        this.Add(keys[i], values[i]);
    //    }
    //}
}