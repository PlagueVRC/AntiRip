using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Kanna.Protecc
{
    public class MenuUtilites
    {
        const string okText = "Ok";
        
        [MenuItem("Tools/Kanna Protecc/Unlock All Materials In Hierarchy...", false)]
        public static void UnlockAllMaterialsInHierarchy(MenuCommand command)
        {
            KannaProteccRoot.Instance.IsProtected = false;

            const string message = "Select a Root GameObject which has children with locked materials.";
            
            void ErrorDialogue()
            {
                EditorUtility.DisplayDialog("No GameObject Selected!",
                    message,
                    okText);
            }
            
            if (Selection.objects.Length == 0)
            {
                ErrorDialogue();
                return;
            }

            var gameObject = (Selection.objects[0] as GameObject);
            
            if (gameObject == null)
            {
                ErrorDialogue();
                return;
            }

            var skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var renderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);

            if (skinnedMeshRenderers.Length == 0 && renderers.Length == 0)
            {
                EditorUtility.DisplayDialog("No SkinnedMeshRenderers nor MeshRenderers found!",
                    message,
                    okText);
                return;
            }

            var Mats = new List<Material>();

            var ProcessedMats = new List<Material>();
            var ProcessedShaders = new List<string>();

            foreach (var renderer in skinnedMeshRenderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || ProcessedMats.Contains(material))
                    {
                        continue;
                    }

                    try
                    {
                        ProcessedMats.Add(material);

                        var path = AssetDatabase.GetAssetPath(material.shader);

                        if (ProcessedShaders.Contains(path))
                        {
                            continue;
                        }

                        ProcessedShaders.Add(path);

                        if (path.Contains("_Protected.shader") && File.Exists(path.Replace("_Protected.shader", ".shader")))
                        {
                            var AllIncludes = material.shader.FindAllShaderIncludes();

                            material.shader = AssetDatabase.LoadAssetAtPath<Shader>(path.Replace("_Protected.shader", ".shader"));

                            File.Delete(path);

                            foreach (var include in AllIncludes)
                            {
                                if (include.Contains("_Protected") || include.Contains("KannaModelDecode"))
                                {
                                    File.Delete(include);
                                }
                            }
                        }

                        if (IsShaderLockable(material.shader) && material.shader.name.Contains("Locked"))
                        {
                            Mats.Add(material);
                        }
                    }
                    catch (Exception e)
                    {
                        KannaLogger.LogToFile($"Error Unlocking skinnedMeshRenderer Material {material.name}: {e}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Error);
                    }
                }
            }
            
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || ProcessedMats.Contains(material))
                    {
                        continue;
                    }

                    try
                    {
                        ProcessedMats.Add(material);

                        var path = AssetDatabase.GetAssetPath(material.shader);

                        if (ProcessedShaders.Contains(path))
                        {
                            continue;
                        }

                        ProcessedShaders.Add(path);

                        if (path.Contains("_Protected.shader") && File.Exists(path.Replace("_Protected.shader", ".shader")))
                        {
                            var AllIncludes = material.shader.FindAllShaderIncludes();

                            material.shader = AssetDatabase.LoadAssetAtPath<Shader>(path.Replace("_Protected.shader", ".shader"));

                            File.Delete(path);

                            foreach (var include in AllIncludes)
                            {
                                if (include.Contains("_Protected") || include.Contains("KannaModelDecode"))
                                {
                                    File.Delete(include);
                                }
                            }
                        }

                        if (IsShaderLockable(material.shader) && material.shader.name.Contains("Locked"))
                        {
                            Mats.Add(material);
                        }
                    }
                    catch (Exception e)
                    {
                        KannaLogger.LogToFile($"Error Unlocking renderer Material {material.name}: {e}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Error);
                    }
                }
            }
            
            if (Mats.Count == 0)
            {
                EditorUtility.DisplayDialog("All materials already unlocked in hiearchy!",
                    message,
                    okText);
                return;
            }

            AssetDatabase.Refresh();
            
            SetLockedForAllMaterials(Mats.ToArray(), false, true, false, false);

            AssetDatabase.Refresh();
        }

        private static bool IsShaderLockable(Shader shader)
        {
            var optimizer = KannaProteccRoot.GetTypeFromAnyAssembly("Thry.ShaderOptimizer");

            if (optimizer != null)
            {
                return ((bool?)optimizer.GetMethod("IsShaderUsingThryOptimizer", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { shader })) ?? false;
            }
            else
            {
                Debug.LogWarning("Thry Is Not In Your Project, So Kanna Protecc Will Assume All Shaders Do Not Support Locking!");
            }

            return false;
        }

        private static void SetLockedForAllMaterials(Material[] mats, bool locked, bool showProgressBar = false, bool showDialog = false, bool allowCancel = true)
        {
            var optimizer = KannaProteccRoot.GetTypeFromAnyAssembly("Thry.ShaderOptimizer");

            if (optimizer != null)
            {
                optimizer.GetMethod("SetLockedForAllMaterials", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { mats, locked ? 1 : 0, showProgressBar, showDialog, allowCancel, null });
            }
            else
            {
                Debug.LogWarning("Thry Is Not In Your Project, So Kanna Protecc Will Assume All Shaders Do Not Support Locking!");
            }
        }

        //[MenuItem("Tools/Kanna Protecc/Check for Update...", false)]
        //static void CheckForUpdate()
        //{
        //    var list = UnityEditor.PackageManager.Client.List();
        //    while (!list.IsCompleted)
        //    { }
        //    PackageInfo package = list.Result.FirstOrDefault(q => q.name == "com.Kanna.Protecc");
        //    if (package == null)
        //    {
        //        EditorUtility.DisplayDialog("Not installed via UPM!",
        //            "This upgrade option only works if you installed via UPM. Go to AvaCrypt github and reinstall via UPM if you wish to use this",
        //            okText);
        //        return;
        //    }

        //    UnityEditor.PackageManager.Client.Add("https://github.com/PlagueVRC/AntiRip.git");
        //}
    }
}
