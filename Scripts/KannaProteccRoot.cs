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
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using System.Reflection;
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

        [Header("Set high enough so your encrypted mesh is visuall. Default = .5")]
        [Range(.6f, 5f)]
        [SerializeField]
        float _distortRatio = 5f;

        [Header("Ensure this is pointing to your LocalAvatarData folder!")]
        [SerializeField]
        string _vrcSavedParamsPath = string.Empty;

        [Header("Materials in this list will also be locked and injected.")]
        [SerializeField]
        public List<Material> m_AdditionalMaterials = new List<Material>();

        [Header("Materials in this list will be ignored.")]
        [SerializeField]
        public List<Material> m_IgnoredMaterials = new List<Material>();

        [SerializeField]
        public bool[] _bitKeys = new bool[32];

        public readonly string pathPrefix = "Assets/Kanna/Obfuscated Files/";

        public static readonly string LogLocation = "KannaProteccLog.html";

        [SerializeField]
        public string path = "";

        [SerializeField]
        public bool disableObjectNameObfuscation = false;

        [SerializeField]
        public List<Object> excludeObjectNames = new List<Object>();

        [SerializeField]
        public List<string> excludeParamNames = new List<string>();

        public enum AnimLayerType
        {
            Base = 0,
            //Deprecated0 = 1,
            Additive = 2,
            Gesture = 3,
            Action = 4,
            FX = 5,
            Sitting = 6,
            TPose = 7,
            IKPose = 8,
        }

        [SerializeField]
        public List<AnimLayerType> excludeAnimatorLayers = new List<AnimLayerType>();

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
            KannaLogger.LogToFile($"Validating Animator Controller: {obj.name}: {controller.name}", LogLocation);

            _KannaProteccController.InitializeCount(_bitKeys.Length);
            _KannaProteccController.ValidateAnimations(obj, controller);
            _KannaProteccController.ValidateParameters(controller);
            _KannaProteccController.ValidateLayers(obj, controller);

            KannaLogger.LogToFile($"Obfuscating Kanna Protecc Layer For Controller: {obj.name}: {controller.name}", LogLocation);

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
            if (runtimeController == null)
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

        public static bool IsMeshSupported(Mesh mesh)
        {
            var existingMeshPath = AssetDatabase.GetAssetPath(mesh);

            return !string.IsNullOrEmpty(existingMeshPath) && !existingMeshPath.Contains("unity default resources");
        }

        public void EncryptAvatar()
        {
            if (ParameterRenamedValues.Count == 0 && !string.IsNullOrEmpty(GetComponent<PipelineManager>()?.blueprintId))
            {
                if (EditorUtility.DisplayDialog("Error!", "Do Not Encrypt A Previously Non-Encrypted Avatar That Was Uploaded! Detach The Blueprint ID And Upload This To A New One!", "Okay", "Bypass Warning"))
                {
                    return;
                }
            }

            if (File.Exists(LogLocation))
            {
                File.Delete(LogLocation); // Remove Old Log
                KannaLogger.LogCache.Clear();
            }

            Utilities.ResetRandomizer();

            var newName = gameObject.name.Trim() + "_Encrypted";

            // delete old GO, do as such in case its disabled
            var scene = SceneManager.GetActiveScene();
            var sceneRoots = scene.GetRootGameObjects();
            foreach (var oldGameObject in sceneRoots)
            {
                if (oldGameObject.name.Trim() == newName)
                {
                    KannaLogger.LogToFile($"Destroying Old Encrypted Object: {newName}", LogLocation);
                    DestroyImmediate(oldGameObject);
                }
            }

            var encodedGameObject = Instantiate(gameObject);
            encodedGameObject.name = newName;
            encodedGameObject.SetActive(true);

            var data = new KannaProteccData(_bitKeys.Length);
            var decodeShader = KannaProteccMaterial.GenerateDecodeShader(data, _bitKeys);

            KannaLogger.LogToFile($"Initialized, Getting All Meshes..", LogLocation);

            var meshFilters = encodedGameObject.GetComponentsInChildren<MeshFilter>(true).Select(o => (o, o.gameObject.activeSelf));
            var skinnedMeshRenderers = encodedGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(o => (o, o.gameObject.activeSelf));

            KannaLogger.LogToFile($"Got All Meshes, Encrypting Additional Materials..", LogLocation);

            EncryptMaterials(null, m_AdditionalMaterials.ToArray(), decodeShader, m_IgnoredMaterials);

            KannaLogger.LogToFile($"Additional Materials Encrypted, Processing MeshFilters..", LogLocation);

            // Do encrypting
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.o.GetComponent<MeshRenderer>() != null)
                {
                    meshFilter.o.gameObject.SetActive(true);
                    var materials = meshFilter.o.GetComponent<MeshRenderer>().sharedMaterials;
                    if (EncryptMaterials(meshFilter.o.sharedMesh, materials, decodeShader, m_IgnoredMaterials))
                    {
                        meshFilter.o.sharedMesh = KannaProteccMesh.EncryptMesh(meshFilter.o.GetComponent<MeshRenderer>(), meshFilter.o.sharedMesh, _distortRatio, data, m_IgnoredMaterials) ?? meshFilter.o.sharedMesh;
                    }
                    else
                    {
                        KannaLogger.LogToFile($"Ignoring Encrypt On Generic: {meshFilter.o.gameObject.name} - No Materials Encrypted", LogLocation, KannaLogger.LogType.Warning);
                    }

                    meshFilter.o.gameObject.SetActive(meshFilter.activeSelf);
                }
                else
                {
                    KannaLogger.LogToFile("WTF? MeshFilter Lacks A Renderer! -> " + meshFilter.o.name, LogLocation, KannaLogger.LogType.Error);
                }
            }

            KannaLogger.LogToFile($"MeshFilters Done, Processing SkinnedMeshRenderers..", LogLocation);

            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                if (skinnedMeshRenderer.o.GetComponent<Cloth>() == null)
                {
                    skinnedMeshRenderer.o.gameObject.SetActive(true);
                    var materials = skinnedMeshRenderer.o.sharedMaterials;
                    if (EncryptMaterials(skinnedMeshRenderer.o.sharedMesh, materials, decodeShader, m_IgnoredMaterials))
                    {
                        skinnedMeshRenderer.o.sharedMesh = KannaProteccMesh.EncryptMesh(skinnedMeshRenderer.o, skinnedMeshRenderer.o.sharedMesh, _distortRatio, data, m_IgnoredMaterials) ?? skinnedMeshRenderer.o.sharedMesh;
                    }
                    else
                    {
                        KannaLogger.LogToFile($"Ignoring Encrypt On Skinned: {skinnedMeshRenderer.o.gameObject.name} - No Materials Encrypted", LogLocation, KannaLogger.LogType.Warning);
                    }

                    skinnedMeshRenderer.o.gameObject.SetActive(skinnedMeshRenderer.activeSelf);
                }
                else
                {
                    KannaLogger.LogToFile($"Ignoring Encrypt On {skinnedMeshRenderer.o.gameObject.name} - Cloth Found.", LogLocation, KannaLogger.LogType.Warning);
                }
            }

            KannaLogger.LogToFile($"SkinnedMeshRenderers Done, Removing Lingering KannaProteccRoot On Encrypted Object..", LogLocation);

            var KannaProteccRoots = encodedGameObject.GetComponentsInChildren<KannaProteccRoot>(true);
            foreach (var KannaProteccRoot in KannaProteccRoots)
            {
                DestroyImmediate(KannaProteccRoot);
            }

            KannaLogger.LogToFile($"Done Removing, Disabling Original Avatar Object And Refreshing AssetDatabase..", LogLocation);

            // Disable old for convienence.
            gameObject.SetActive(false);

            // Force unity to import things
            AssetDatabase.Refresh();

            KannaLogger.LogToFile($"Done Refreshing, Beginning Obfuscation Stage..", LogLocation);

            IsProtected = true;

            // Do Obfuscation
            var newobj = obfuscator.Obfuscate(encodedGameObject, this);

            KannaLogger.LogToFile($"Done Obfuscating, Disabling Encrypted Object, Saving Assets And Scene, Then Refreshing AssetDatabase.", LogLocation);

            encodedGameObject.SetActive(false); // Temp

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            // Force unity to import things
            AssetDatabase.Refresh();

            KannaLogger.LogToFile($"Done Refreshing, Writing Keys To ExpressionParameters", LogLocation);

            WriteBitKeysToExpressions(newobj.GetComponent<VRCAvatarDescriptor>().expressionParameters, true);

            KannaLogger.LogToFile($"Done Writing Keys, Validating FX Controller And Obfuscating Kanna Protecc Layer Within It", LogLocation);

            ValidateAnimatorController(newobj, AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(newobj.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers.First(o => o.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController)));

            KannaLogger.LogToFile($"Done! Showing Dialog To User.", LogLocation);

            DestroyImmediate(encodedGameObject);
            newobj.name = newobj.name.Replace("_Encrypted_Obfuscated", "_KannaProteccted");

            KannaLogger.WriteLogsToFile(LogLocation);

            EditorUtility.DisplayDialog("Successfully Encrypted!", $"{(string.IsNullOrEmpty(GetComponent<PipelineManager>()?.blueprintId) ? "" : "Keys were automatically written. ")}Your avatar should be ready to upload!", "Okay");
        }

        public static Type GetTypeFromAnyAssembly(string FullName)
        {
            KannaLogger.LogToFile($"Getting Type From FullName: {FullName}", LogLocation);

            return (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                    from type in assembly.GetTypes()
                    where type.FullName == FullName
                    select type).FirstOrDefault();
        }

        bool EncryptMaterials(Mesh mesh, Material[] materials, string decodeShader, List<Material> aggregateIgnoredMaterials)
        {
            if (mesh != null && !IsMeshSupported(mesh))
            {
                var existingMeshPath = AssetDatabase.GetAssetPath(mesh);

                KannaLogger.LogToFile($"Asset For Mesh Not Found, Invalid Or Is A Built In Unity Mesh! -> {mesh.name}: {existingMeshPath ?? ""}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Warning);

                return false;
            }

            KannaLogger.LogToFile($"EncryptMaterials Start..", LogLocation);
            var materialEncrypted = false;

            if (Utilities.GetThry())
            {
                var lockablemats = materials.Where(o => Utilities.CanShaderBeLocked(o.shader) && !Utilities.IsMaterialLocked(o)).ToArray();

                if (lockablemats.Length > 0)
                {
                    KannaLogger.LogToFile($"Some Shaders Support Locking And Are Not Locked, Locking..", LogLocation);

                    Utilities.SetAllChildShadersLockedState(true, gameObject);
                }
            }

            foreach (var mat in materials)
            {
                if (mat != null && mat.shader != null/* && KannaProteccMaterial.IsShaderSupported(mat.shader, out var shaderMatch)*/)
                {
                    if (mat.shader.name.Contains("KannaProtecc"))
                    {
                        materialEncrypted = true;
                        continue;
                    }

                    KannaLogger.LogToFile($"Found Supported Material: {mat.name} With Shader: {mat.shader.name}", LogLocation);

                    if (aggregateIgnoredMaterials.Contains(mat))
                    {
                        KannaLogger.LogToFile($"Material: {mat.name} Is In IgnoredMaterials, Skipping..", LogLocation, KannaLogger.LogType.Warning);
                        continue;
                    }

                    var shaderPath = AssetDatabase.GetAssetPath(mat.shader);

                    if (!shaderPath.Contains("Assets"))
                    {
                        KannaLogger.LogToFile($"Ignoring Encrypt Of Shader: {mat.shader.name} As It Is Not In Assets!", LogLocation, KannaLogger.LogType.Warning);
                        continue;
                    }

                    if (Utilities.GetThry() && Utilities.CanShaderBeLocked(mat.shader) && !Utilities.IsMaterialLocked(mat)) // Double Check
                    {
                        KannaLogger.LogToFile($"{mat.name} {mat.shader.name} Trying To Inject Non-Locked Shader?! - Skipping!", LogLocation, KannaLogger.LogType.Error);
                        continue;
                    }

                    KannaLogger.LogToFile($"Writing KannaModelDecode For {mat.shader.name}", LogLocation);

                    var path = Path.GetDirectoryName(shaderPath);
                    var decodeShaderPath = Path.Combine(path, "KannaModelDecode.cginc");
                    File.WriteAllText(decodeShaderPath, decodeShader);

                    KannaLogger.LogToFile($"Done Writing, Processing Shader Contents..", LogLocation);

                    var shaderText = File.ReadAllText(shaderPath);

                    if (shaderText.Contains("appdata_base") || shaderText.Contains("appdata_tan") || shaderText.Contains("appdata_full"))
                    {
                        continue;
                    }

                    ShaderInfoLib.KannaShaderInfo shaderInfo = null;

                    try
                    {
                        shaderInfo = ShaderInfoLib.GetShaderInfo(shaderText);
                    }
                    catch
                    {

                    }

                    if (!shaderText.Contains("//KannaProtecc Injected"))
                    {
                        KannaLogger.LogToFile($"{mat.shader.name} Not Yet Injected, Injecting..", LogLocation);

                        _sb.Clear();
                        _sb.AppendLine("//KannaProtecc Injected");

                        var indexofshadername = shaderText.IndexOf("Shader \"", StringComparison.Ordinal) + "Shader \"".Length;

                        var textafter = shaderText.Substring(indexofshadername);

                        var shadername = textafter.Substring(0, textafter.IndexOf("\"", StringComparison.Ordinal));

                        _sb.Append(shaderText.Replace($"Shader \"{shadername}\"", $"Shader \"{shadername}/KannaProtecc\""));

                        if (shaderInfo != null)
                        {
                            _sb.ReplaceOrLog(shaderInfo.VertMethod, "#include \"KannaModelDecode.cginc\"\r\n{OrigText}"); // Vert Include Addition

                            if (shaderInfo.UVs.Length > 0)
                            {
                                foreach (var uv in shaderInfo.UVs)
                                {
                                    var uvsplitlines = uv.Definition.Split('\n').ToList();
                                    uvsplitlines.Insert(uvsplitlines.Count - 2, "float3 uv6: TEXCOORD6;");
                                    uvsplitlines.Insert(uvsplitlines.Count - 2, "float3 uv7: TEXCOORD7;");

                                    _sb.ReplaceOrLog(new[] { uv.Definition }, string.Join("\r\n", uvsplitlines));
                                    _sb.ReplaceOrLog(new[] { $"const {uv.Name}" }, $"{uv.Name}"); // yeet const
                                }

                                _sb.ReplaceOrLog(new[] { "UNITY_SETUP_INSTANCE_ID(v);", "UNITY_SETUP_INSTANCE_ID( v );", "UNITY_SETUP_INSTANCE_ID(v );", "UNITY_SETUP_INSTANCE_ID( v);" }, $"v.{shaderInfo.UVs[0].VertexPropertyName} = modelDecode(v.{shaderInfo.UVs[0].VertexPropertyName}, v.normal, v.uv6, v.uv7);\r\n{{OrigText}}");
                            }
                            else
                            {
                                _sb.ReplaceOrLog(new[] { "UNITY_SETUP_INSTANCE_ID(v);", "UNITY_SETUP_INSTANCE_ID( v );", "UNITY_SETUP_INSTANCE_ID(v );", "UNITY_SETUP_INSTANCE_ID( v);" }, $"v.vertex = modelDecode(v.vertex, v.normal, v.uv6, v.uv7);\r\n{{OrigText}}");
                            }
                        }
                        KannaLogger.LogToFile($"Done, Writing Shader File..", LogLocation);

                        shaderPath = shaderPath.Replace(Path.GetExtension(shaderPath), "") + "_Protected.shader";

                        File.WriteAllText(shaderPath, _sb.ToString());

                        KannaLogger.LogToFile($"Done, Refreshing AssetDatabase..", LogLocation);

                        AssetDatabase.Refresh();

                        KannaLogger.LogToFile($"Done, Assigning New Shader To {mat.name} And Assigning VRCFallback Hidden Tag..", LogLocation);

                        mat.shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

                        mat.SetOverrideTag("VRCFallback", "Hidden");
                    }

                    AssetDatabase.Refresh();

                    KannaLogger.LogToFile($"Done Handling Injection, Handling Shader Includes..", LogLocation);

                    var IncludeFileDirs = new List<string>();

                    foreach (var include in mat.shader.FindAllShaderIncludes().Where(o => !o.Contains("KannaModelDecode.cginc")))
                    {
                        var includeText = File.ReadAllText(include);

                        if (includeText.Contains("appdata_base") || includeText.Contains("appdata_tan") || includeText.Contains("appdata_full"))
                        {
                            continue;
                        }

                        ShaderInfoLib.KannaShaderInfo shaderInfo2 = null;

                        try
                        {
                            shaderInfo2 = ShaderInfoLib.GetShaderInfo(includeText, shaderInfo.VertMethod);
                        }
                        catch
                        {

                        }

                        if (!includeText.Contains("//KannaProtecc Injected"))
                        {
                            KannaLogger.LogToFile($"Include File: {include} Not Yet Injected, Injecting..", LogLocation);

                            _sb.Clear();
                            _sb.AppendLine("//KannaProtecc Injected\r\n");
                            _sb.Append(includeText);

                            if (shaderInfo2 != null)
                            {
                                _sb.ReplaceOrLog(shaderInfo2.VertMethod, "#include \"KannaModelDecode.cginc\"\r\n{OrigText}"); // Vert Include Addition

                                if (shaderInfo2.UVs.Length > 0)
                                {
                                    foreach (var uv in shaderInfo2.UVs)
                                    {
                                        var uvsplitlines = uv.Definition.Split('\n').ToList();
                                        uvsplitlines.Insert(uvsplitlines.Count - 2, "float3 uv6: TEXCOORD6;");
                                        uvsplitlines.Insert(uvsplitlines.Count - 2, "float3 uv7: TEXCOORD7;");

                                        _sb.ReplaceOrLog(new[] { uv.Definition }, string.Join("\r\n", uvsplitlines));
                                        _sb.ReplaceOrLog(new[] { $"const {uv.Name}" }, $"{uv.Name}"); // yeet const
                                    }

                                    _sb.ReplaceOrLog(new[] { "UNITY_SETUP_INSTANCE_ID(v);", "UNITY_SETUP_INSTANCE_ID( v );", "UNITY_SETUP_INSTANCE_ID(v );", "UNITY_SETUP_INSTANCE_ID( v);" }, $"v.{shaderInfo2.UVs[0].VertexPropertyName} = modelDecode(v.{shaderInfo2.UVs[0].VertexPropertyName}, v.normal, v.uv6, v.uv7);\r\n{{OrigText}}");
                                }
                                else
                                {
                                    _sb.ReplaceOrLog(new[] { "UNITY_SETUP_INSTANCE_ID(v);", "UNITY_SETUP_INSTANCE_ID( v );", "UNITY_SETUP_INSTANCE_ID(v );", "UNITY_SETUP_INSTANCE_ID( v);" }, $"v.vertex = modelDecode(v.vertex, v.normal, v.uv6, v.uv7);\r\n{{OrigText}}");
                                }
                            }

                            var newFileName = include.Replace(Path.GetExtension(include), "") + $"_Protected{Path.GetExtension(include)}";
                            IncludeFileDirs.Add(newFileName);
                            File.WriteAllText(newFileName, _sb.ToString());

                            KannaLogger.LogToFile($"Done, Written Modified Include To {newFileName}", LogLocation);
                        }
                    }

                    var FileText = File.ReadAllText(shaderPath);

                    foreach (var dir in IncludeFileDirs)
                    {
                        var newName = Path.GetFileName(dir);

                        KannaLogger.LogToFile($"Adjusting Include: {newName.Replace("_Protected", "")} To {newName} In {shaderPath}..", LogLocation);

                        FileText = FileText.Replace(newName.Replace("_Protected", ""), newName);

                        KannaLogger.LogToFile($"Done, Now Handling Recursive Includes..", LogLocation);

                        foreach (var towrite in IncludeFileDirs) // write all new include names
                        {
                            var newName2 = Path.GetFileName(towrite);

                            File.WriteAllText(dir, File.ReadAllText(dir).Replace(newName2.Replace("_Protected", ""), newName2));
                        }
                    }

                    KannaLogger.LogToFile($"Done, Writing Shader Contents To {shaderPath}..", LogLocation);

                    File.WriteAllText(shaderPath, FileText);

                    materialEncrypted = true;
                }
            }

            return materialEncrypted;
        }

        public void WriteBitKeysToExpressions(VRCExpressionParameters ExtraParams = null, bool WriteOnlyToExtra = false, bool DoLog = false)
        {
            KannaLogger.LogToFile($"WriteBitKeysToExpressions Started", KannaProteccRoot.LogLocation);

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
                KannaLogger.LogToFile($"WriteBitKeysToExpressions Halted - No Blueprint ID On Object: {gameObject.name}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Warning);
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

        public class KannaProteccKeysData
        {
            public string AvatarID = "Invalid";
            public Dictionary<string, object> Values = new Dictionary<string, object>();
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
                EditorUtility.DisplayDialog("Keys not written! Blueprint ID not filled in!", "You need to first populate your PipelineManager with a Blueprint ID before keys can be written. Publish your avatar to get the Blueprint ID, attach the ID through the PipelineManager then run Write Keys again.", "Okay");
                return;
            }

            if (!Directory.Exists(_vrcSavedParamsPath))
            {
                Debug.LogError("Keys not written! Could not find VRC LocalAvatarData folder!");
                EditorUtility.DisplayDialog("Could not find VRC LocalAvatarData folder!", "Ensure the VRC Saved Params Path is point to your LocalAvatarData folder, should be at C:\\Users\\username\\AppData\\LocalLow\\VRChat\\VRChat\\LocalAvatarData\\, then run Write Keys again.", "Okay");
                return;
            }

            foreach (var userDir in Directory.GetDirectories(_vrcSavedParamsPath))
            {
                var filePath = $"{userDir}\\{pipelineManager.blueprintId}";
                KannaLogger.LogToFile($"Writing keys to {filePath}", KannaProteccRoot.LogLocation);
                ParamFile paramFile = null;
                if (File.Exists(filePath))
                {
                    KannaLogger.LogToFile($"Avatar param file already exists, loading and editing.", KannaProteccRoot.LogLocation);
                    var json = File.ReadAllText(filePath);
                    paramFile = JsonUtility.FromJson<ParamFile>(json);

                    if (paramFile.animationParameters.Any(o => o.name.Length > 16))
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

                using (var client = new HttpClient())
                {
                    client.PostAsync("http://127.0.0.1:3456/protecc", new StringContent(JsonConvert.SerializeObject(new KannaProteccKeysData()
                    {
                        AvatarID = pipelineManager.blueprintId,
                        Values = paramFile.animationParameters.Select(o => new KeyValuePair<string, object>(o.name, o.value == 1 ? true : false)).ToDictionary(a => a.Key, b => b.Value)
                    })));
                }
            }

            if (DoLog)
                EditorUtility.DisplayDialog("Successfully Wrote Keys!", "Your avatar should now just work in VRChat. If you accidentally hit 'Reset Avatar' in VRC 3.0 menu, you need to click Write Keys again.", "Okay");

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
            var parameters = (VRCExpressionParameters)command.context;
            AddBitKeys(parameters, Instance);
        }

        public static bool AddBitKeys(VRCExpressionParameters parameters, KannaProteccRoot root, bool RemoveOld = true)
        {
            if (RemoveOld)
            {
                //RemoveBitKeys(parameters);
            }
            KannaLogger.LogToFile($"Adding BitKeys To Parameters: {parameters.name}", LogLocation);

            var paramList = parameters.parameters.ToList();

            for (var i = 0; i < root._bitKeys.Length; ++i)
            {
                var bitKeyName = root.GetBitKeyName(i);

                var index = Array.FindIndex(parameters.parameters, p => p.name == bitKeyName);
                if (index != -1)
                {
                    KannaLogger.LogToFile($"Found BitKey In Params: {bitKeyName}", LogLocation);
                    parameters.parameters[index].saved = true;
                    parameters.parameters[index].defaultValue = 0;
                    parameters.parameters[index].valueType = VRCExpressionParameters.ValueType.Bool;
                }
                else
                {
                    KannaLogger.LogToFile($"Adding BitKey In Params: {bitKeyName}", LogLocation);
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
            KannaLogger.LogToFile($"Remaining Cost: {remainingCost}", LogLocation);
            if (remainingCost < 0)
            {
                KannaLogger.LogToFile("Adding BitKeys took up too many parameters!", LogLocation, KannaLogger.LogType.Error);
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
        public static List<Transform> GetAllChildren(this Transform parent, bool recursive)
        {
            var list = new List<Transform>();

            if (recursive)
            {
                list.AddRange(parent.GetComponentsInChildren<Transform>(true));
            }
            else
            {
                for (var i = 0; i < parent.childCount; i++)
                {
                    list.Add(parent.GetChild(i));
                }
            }

            return list;
        }

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

            //Debug.Log(absolutePath);

            return absolutePath;
        }

#if UNITY_EDITOR
        public static List<string> FindAllShaderIncludes(this Shader shader)
        {
            var FoundIncludes = new List<string>();

            var ShaderText = File.ReadAllText(AssetDatabase.GetAssetPath(shader));

            FindIncludes(ShaderText, Path.GetDirectoryName(AssetDatabase.GetAssetPath(shader)));

            void FindIncludes(string searchMe, string dir)
            {
                if (searchMe.IndexOf("#include ", StringComparison.Ordinal) > -1)
                {
                    foreach (var line in searchMe.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.IndexOf("#include ", StringComparison.Ordinal) > -1)
                        {
                            var StartIndex = line.IndexOf("#include \"", StringComparison.Ordinal) + "#include \"".Length;
                            var EndIndex = line.LastIndexOf("\"", StringComparison.Ordinal);

                            //Debug.Log($"Making {line.Substring(StartIndex, EndIndex - StartIndex)} Relative To {dir}");

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
#endif
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

public class KannaLogger
{
    public enum LogType
    {
        Log,
        Warning,
        Error
    }

    public class LogEntry
    {
        public string time;
        public string type;
        public readonly List<StackEntry> Stack = new List<StackEntry>();
        public string text;
    }

    public class StackEntry
    {
        public string FileName;
        public string MethodDeclaringType;
        public string MethodName;
        public int LineNumber;
    }

    public static List<LogEntry> LogCache = new List<LogEntry>();

    public static LogEntry LogToFile(string text, string path, LogType type = LogType.Log, bool DebugLog = true)
    {
        switch (type)
        {
            case LogType.Log:
                Debug.Log(text);
                break;
            case LogType.Warning:
                Debug.LogWarning(text);
                break;
            case LogType.Error:
                Debug.LogError(text);
                break;
        }

        var entry = FormatLog(text, type);

        LogCache.Add(entry);

        return entry;
    }

    public static void WriteLogsToFile(string path)
    {
        var html = LogStart;

        CollapsibleID = 0;

        foreach (var log in LogCache)
        {
            html += CreateLogHTML(log);
        }

        html += LogEnd;

        File.WriteAllText(path, html);
    }

    private static int CollapsibleID;
    private static string CreateLogHTML(LogEntry log)
    {
        var output = $"\t<div class=\"AvatarInformation{log.type}\">\r\n\t\t\t<h2>[{log.time}]: {log.text}</h2>\r\n\t\t\t<div class=\"wrap-collabsible\">\r\n\t\t\t\t<input id=\"collapsible{CollapsibleID}\" class=\"toggle\" type=\"checkbox\">\r\n\t\t\t\t\t<label for=\"collapsible{CollapsibleID}\" class=\"lbl-toggle\">Stack Trace</label>\r\n\t\t\t\t\t<div class=\"collapsible-content\">\r\n\t\t\t\t\t\t<div class=\"content-inner\">\r\n\t\t\t\t\t\t<p>{JsonConvert.SerializeObject(log.Stack, Formatting.Indented).Replace("\r\n", "</br>")}</p>\r\n\t\t\t\t\t\t</div>\r\n\t\t\t\t\t</div>\r\n\t\t\t</div>\r\n\t\t\t</br>\t\t\r\n\t\t</div>\r\n\t";

        CollapsibleID++;

        return output;
    }

    private static string LogStart = "<html>\r\n\t<head>\r\n\t\t<title>Kanna Protecc Log</title>\r\n\t\t\r\n\t\t<style>\r\n\t\t\th1\r\n\t\t\t{\r\n\t\t\t\tcolor: white;\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\th2\r\n\t\t\t{\r\n\t\t\t\tcolor: white;\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\tp\r\n\t\t\t{\r\n\t\t\t\tcolor: white;\r\n\t\t\t}\r\n\t\t\r\n\t\t\t.AvatarInformationLog\r\n\t\t\t{\r\n\t\t\t\tbackground-color: rgb(10, 10, 10);\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.AvatarInformationWarning\r\n\t\t\t{\r\n\t\t\t\tbackground-color: rgb(150, 150, 0);\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.AvatarInformationError\r\n\t\t\t{\r\n\t\t\t\tbackground-color: rgb(150, 0, 0);\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\tinput[type='checkbox'].toggle\r\n\t\t\t{\r\n\t\t\t\tdisplay: none; \r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.wrap-collabsible\r\n\t\t\t{\r\n\t\t\t\tmargin: 1.2rem 0;\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.lbl-toggle\r\n\t\t\t{\r\n\t\t\t\tdisplay: block;\r\n\t\t\t\tfont-weight: bold;\r\n\t\t\t\tfont-family: monospace;\r\n\t\t\t\tfont-size: 1.2rem;\r\n\t\t\t\ttext-transform: uppercase;\r\n\t\t\t\ttext-align: center;\r\n\t\t\t\tpadding: 1rem;\r\n\t\t\t\tcolor: #DDD;\r\n\t\t\t\tbackground: #69696950;\r\n\t\t\t\tcursor: pointer;\r\n\t\t\t\tborder-radius: 7px;\r\n\t\t\t\ttransition: all 0.25s ease-out;\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.lbl-toggle:hover\r\n\t\t\t{\r\n\t\t\t\tcolor: #FFF;\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.lbl-toggle::before\r\n\t\t\t{\r\n\t\t\t\tcontent: ' ';\r\n\t\t\t\tdisplay: inline-block;\r\n\t\t\t\tborder-top: 5px solid transparent;\r\n\t\t\t\tborder-bottom: 5px solid transparent;\r\n\t\t\t\tborder-left: 5px solid currentColor;\r\n\t\t\t\tvertical-align: middle;\r\n\t\t\t\tmargin-right: .7rem;\r\n\t\t\t\ttransform: translateY(-2px);\r\n\t\t\t\ttransition: transform .2s ease-out;\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.toggle:checked+.lbl-toggle::before\r\n\t\t\t{\r\n\t\t\t\ttransform: rotate(90deg) translateX(-3px);\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.collapsible-content\r\n\t\t\t{\r\n\t\t\t\tmax-height: 0px;\r\n\t\t\t\toverflow: hidden;\r\n\t\t\t\ttransition: max-height .25s ease-in-out;\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.toggle:checked + .lbl-toggle + .collapsible-content\r\n\t\t\t{\r\n\t\t\t\tmax-height: 350px;\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.toggle:checked+.lbl-toggle\r\n\t\t\t{\r\n\t\t\t\tborder-bottom-right-radius: 0;\r\n\t\t\t\tborder-bottom-left-radius: 0;\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.collapsible-content .content-inner\r\n\t\t\t{\r\n\t\t\t\tbackground: rgba(0, 105, 255, .2);\r\n\t\t\t\tborder-bottom: 1px solid rgba(0, 105, 255, .45);\r\n\t\t\t\tborder-bottom-left-radius: 7px;\r\n\t\t\t\tborder-bottom-right-radius: 7px;\r\n\t\t\t\tpadding: .5rem 1rem;\r\n\t\t\t}\r\n\t\t\t\r\n\t\t\t.collapsible-content p\r\n\t\t\t{\r\n\t\t\t\tmargin-bottom: 0;\r\n\t\t\t}\r\n\t\t</style>\r\n\t\t\r\n\t\t<script>\r\n\t\t\tfunction togglelogs(checkbox) {\r\n\t\t\t\tlet entries = document.getElementsByClassName(\"AvatarInformationLog\");\r\n\r\n\t\t\t\tfor (let i = 0; i < entries.length; i++) {\r\n\t\t\t\t\tentries[i].hidden = !checkbox.checked;\r\n\t\t\t\t}\r\n\t\t\t}\r\n\r\n\t\t\tfunction togglewarnings(checkbox) {\r\n\t\t\t\tlet entries = document.getElementsByClassName(\"AvatarInformationWarning\");\r\n\r\n\t\t\t\tfor (let i = 0; i < entries.length; i++) {\r\n\t\t\t\t\tentries[i].hidden = !checkbox.checked;\r\n\t\t\t\t}\r\n\t\t\t}\r\n\r\n\t\t\tfunction toggleerrors(checkbox) {\r\n\t\t\t\tlet entries = document.getElementsByClassName(\"AvatarInformationError\");\r\n\r\n\t\t\t\tfor (let i = 0; i < entries.length; i++) {\r\n\t\t\t\t\tentries[i].hidden = !checkbox.checked;\r\n\t\t\t\t}\r\n\t\t\t}\r\n\t\t</script>\r\n\t</head>\r\n\t\r\n\t<body>\r\n\t\t<h1>Kanna Protecc Log</h1>\r\n\t\t\r\n\t\t<input class=\"showlogs\" type=\"checkbox\" onclick=\"togglelogs(this);\" checked>Show Logs</input>\r\n\t\t<input class=\"showwarnings\" type=\"checkbox\" onclick=\"togglewarnings(this);\" checked>Show Warnings</input>\r\n\t\t<input class=\"showerrors\" type=\"checkbox\" onclick=\"toggleerrors(this);\" checked>Show Errors</input>\r\n\t\t\r\n\t";

    private static string LogEnd = "</body>\r\n</html>";

    public static LogEntry FormatLog(string text, LogType type = LogType.Log)
    {
        var stackTrace = new StackTrace(fNeedFileInfo: true);

        var entry = new LogEntry
        {
            time = DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt"),
            type = type.ToString(),
            text = text
        };

        foreach (var frame in stackTrace.GetFrames().Skip(2))
        {
            if (entry.Stack.Count == 2)
            {
                break;
            }

            entry.Stack.Add(new StackEntry
            {
                FileName = frame.GetFileName(),
                MethodDeclaringType = $"{frame.GetMethod().DeclaringType?.Namespace}.{frame.GetMethod().DeclaringType?.Name}",
                MethodName = frame.GetMethod().Name,
                LineNumber = frame.GetFileLineNumber()
            });
        }

        return entry;
    }
}

internal static class HTMLSanitizer
{
    internal static string Sanitize(this string htmlshit)
    {
        if (string.IsNullOrEmpty(htmlshit))
        {
            return "";
        }

        string removeChars = "&^$#@!()+-,:;<>�\'-_*/\\";

        string result = htmlshit;

        foreach (char c in removeChars)
        {
            result = result.Replace(c.ToString(), string.Empty);
        }

        return result;
    }
}

public class ShaderInfoLib
{
    public class KannaShaderInfo
    {
        public string[] VertMethod;
        public UV[] UVs;
    }

    public class UV
    {
        public string Name;
        public string Definition;
        public string VertexPropertyName;
    }

    public static KannaShaderInfo GetShaderInfo(string contents, string[] vertsToAppend = null)
    {
        var verts = new List<string>();
        var uvs = new List<UV>();

        if (vertsToAppend != null)
        {
            verts.AddRange(vertsToAppend);
        }

        var subshader = contents.IndexOf("subshader", StringComparison.OrdinalIgnoreCase);

        var subshaderregion = contents.Substring(subshader == -1 ? 0 : subshader);

    AddVert:
        try
        {
            var vert = GetDefinitionOfMethod(subshaderregion, "vert");

            verts.Add(vert);

            subshaderregion = subshaderregion.Substring(subshaderregion.IndexOf(vert, StringComparison.Ordinal) + vert.Length);

            goto AddVert;
        }
        catch
        {

        }

        foreach (var vert in verts)
        {
            try
            {
                var uvtypename = "";

                try
                {
                    var afterbrace = vert.IndexOf("(", StringComparison.OrdinalIgnoreCase) + 1;

                    var uvstart1 = vert.Substring(afterbrace);
                    var uvstart = uvstart1.Substring(0, uvstart1.IndexOf("\r\n", StringComparison.OrdinalIgnoreCase));

                    if (!uvstart.Contains(")"))
                    {
                        throw new Exception();
                    }

                    var isgay = uvstart.Contains("const");

                    if (isgay)
                    {
                        uvstart = uvstart.Substring(uvstart.IndexOf(" ", StringComparison.OrdinalIgnoreCase) + 1);
                    }

                    uvtypename = uvstart.Substring(0, uvstart.IndexOf(" ", StringComparison.OrdinalIgnoreCase));

                    if (uvtypename.Contains("#"))
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    var afterbrace = vert.IndexOf("(", StringComparison.OrdinalIgnoreCase) + 1;

                    var uvstart1 = vert.Substring(afterbrace);
                    var uvstart = uvstart1.Substring(0, uvstart1.IndexOf(")", StringComparison.OrdinalIgnoreCase));

                    var splituv = uvstart.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                    foreach (var str in splituv)
                    {
                        if (string.IsNullOrWhiteSpace(str) || str.Contains("#"))
                        {
                            continue;
                        }

                        var brr = str.TrimStart();

                        var isgay = brr.StartsWith("const");

                        if (isgay)
                        {
                            brr = brr.Substring(brr.IndexOf(" ", StringComparison.OrdinalIgnoreCase) + 1);
                        }

                        if (!isgay)
                        {
                            uvtypename = brr.Substring(0, brr.Contains(" ") ? brr.IndexOf(" ", StringComparison.OrdinalIgnoreCase) : brr.Length);
                            break;
                        }
                    }
                }

                var vertindenting = "";

                foreach (var character in vert)
                {
                    if (char.IsWhiteSpace(character))
                    {
                        vertindenting += character;
                    }
                    else
                    {
                        break;
                    }
                }

                var indexofstruct = contents.IndexOf($"{vertindenting}struct {uvtypename}", StringComparison.OrdinalIgnoreCase);

                if (indexofstruct == -1)
                {
                    indexofstruct = contents.IndexOf($"struct {uvtypename}", StringComparison.OrdinalIgnoreCase);
                }

                if (indexofstruct != -1)
                {
                    var contentsafteruvdef = contents.Substring(GetLineStartIndex(contents, indexofstruct));

                    var uv = contentsafteruvdef.Substring(0, contentsafteruvdef.IndexOf("};", StringComparison.OrdinalIgnoreCase) + 2);

                    var reeee = uv.RegexIndexOf("float(3|4)(\\r|\\n|\\s)*?(.*?)(\\r|\\n|\\s)*?:(.|\\s)*?POSITION\\d?(.|\\s)*?", RegexOptions.None);

                    var onelinednospacesuv = uv.Substring(reeee.Item1, reeee.Item2);

                    var brrline = onelinednospacesuv.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");

                    var prop = brrline.Substring(brrline.IndexOf("float", StringComparison.OrdinalIgnoreCase) + "float".Length + 1, (brrline.IndexOf("POSITION", StringComparison.Ordinal) - (brrline.IndexOf("float", StringComparison.OrdinalIgnoreCase) + "float".Length + 1)) - 1);

                    uvs.Add(new UV
                    {
                        Name = uvtypename,
                        Definition = uv,
                        VertexPropertyName = prop
                    });
                }
            }
            catch
            {
            }
        }

        return new KannaShaderInfo
        {
            VertMethod = verts.ToArray(),
            UVs = uvs.ToArray()
        };
    }

    private static string GetDefinitionOfMethod(string region, string methodname)
    {
        var location = region.RegexIndexOf(
            $" {methodname}" +
                   $"\\s*?" +
                   $"\\(" +
                   $"(.|\\s)" +
                   $"*?" +
                   $"\\)", RegexOptions.IgnoreCase);

        var textvertandafter = region.Substring(GetLineStartIndex(region, location.Item1));

        var closebracetofind = "\n" + GetLineContainingMatch(textvertandafter, "{").Replace("{", "}");

        var match = textvertandafter.Substring(0, textvertandafter.IndexOf(closebracetofind, StringComparison.OrdinalIgnoreCase) + closebracetofind.Length);

        return match;
    }

    private static int GetLineStartIndex(string input, int currentIndex)
    {
        var lineStartIndex = input.LastIndexOf('\n', currentIndex);

        if (lineStartIndex == -1)
        {
            lineStartIndex = input.LastIndexOf('\r', currentIndex);
        }

        return lineStartIndex + 1;
    }

    private static string GetLineContainingMatch(string input, string searchTerm)
    {
        var startIndex = input.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);

        if (startIndex >= 0)
        {
            var lineStartIndex = input.LastIndexOf('\n', startIndex) + 1;
            var lineEndIndex = input.IndexOf('\n', startIndex);
            if (lineEndIndex == -1)
            {
                lineEndIndex = input.Length - 1;
            }

            var matchedLine = input.Substring(lineStartIndex, lineEndIndex - lineStartIndex);
            return matchedLine;
        }

        return null;
    }
}

public static class Ext
{
    public static (int, int) RegexIndexOf(this string input, string pattern, RegexOptions comparisonType)
    {
        var index = (-1, -1);

        var match = Regex.Match(input, pattern, comparisonType);

        if (match.Success)
        {
            index = (match.Index, match.Value.Length);
        }

        return index;
    }
}