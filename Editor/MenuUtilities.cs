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

                            foreach (var include in AllIncludes)
                            {
                                if (include.Contains("_Protected") || include.Contains("KannaModelDecode"))
                                {
                                    File.Delete(include);
                                }
                            }
                            
                            material.shader = AssetDatabase.LoadAssetAtPath<Shader>(path.Replace("_Protected.shader", ".shader"));

                            EditorUtility.SetDirty(material);

                            File.Delete(path);
                        }

                        if (Utilities.CanShaderBeLocked(material.shader) && Utilities.IsMaterialLocked(material))
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

                            foreach (var include in AllIncludes)
                            {
                                if (include.Contains("_Protected") || include.Contains("KannaModelDecode"))
                                {
                                    File.Delete(include);
                                }
                            }
                            
                            material.shader = AssetDatabase.LoadAssetAtPath<Shader>(path.Replace("_Protected.shader", ".shader"));

                            EditorUtility.SetDirty(material);

                            File.Delete(path);
                        }

                        if (Utilities.CanShaderBeLocked(material.shader) && Utilities.IsMaterialLocked(material))
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

            foreach (var material in KannaProteccRoot.Instance.m_AdditionalMaterials)
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

                        foreach (var include in AllIncludes)
                        {
                            if (include.Contains("_Protected") || include.Contains("KannaModelDecode"))
                            {
                                File.Delete(include);
                            }
                        }
                        
                        material.shader = AssetDatabase.LoadAssetAtPath<Shader>(path.Replace("_Protected.shader", ".shader"));

                        EditorUtility.SetDirty(material);

                        File.Delete(path);
                    }

                    if (Utilities.CanShaderBeLocked(material.shader) && Utilities.IsMaterialLocked(material))
                    {
                        Mats.Add(material);
                    }
                }
                catch (Exception e)
                {
                    KannaLogger.LogToFile($"Error Unlocking renderer Material {material.name}: {e}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Error);
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

            Utilities.SetShadersLockedState(Mats.ToArray(), false);

            AssetDatabase.Refresh();
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
