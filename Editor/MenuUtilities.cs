﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Thry;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

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
            
            foreach (var renderer in skinnedMeshRenderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    var path = AssetDatabase.GetAssetPath(material.shader);

                    if (path.Contains("_Protected.shader") && File.Exists(path.Replace("_Protected.shader", ".shader")))
                    {
                        var ProtecctedFilePath = AssetDatabase.GetAssetPath(material.shader);

                        material.shader = AssetDatabase.LoadAssetAtPath<Shader>(ProtecctedFilePath.Replace("_Protected.shader", ".shader"));

                        var AllIncludes = AssetDatabase.LoadAssetAtPath<Shader>(ProtecctedFilePath).FindAllShaderIncludes();

                        File.Delete(ProtecctedFilePath);

                        foreach (var include in AllIncludes)
                        {
                            if (include.Contains("_Protected") || include.Contains("KannaModelDecode"))
                            {
                                File.Delete(include);
                            }
                        }
                    }

                    if (KannaProteccMaterial.IsShaderSupported(material.shader, out var shaderMatch) && shaderMatch.SupportsLocking && material.shader.name.Contains("Locked"))
                    {
                        Mats.Add(material);
                    }
                }
            }
            
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    var path = AssetDatabase.GetAssetPath(material.shader);

                    if (path.Contains("_Protected.shader") && File.Exists(path.Replace("_Protected.shader", ".shader")))
                    {
                        var ProtecctedFilePath = AssetDatabase.GetAssetPath(material.shader);

                        material.shader = AssetDatabase.LoadAssetAtPath<Shader>(ProtecctedFilePath.Replace("_Protected.shader", ".shader"));

                        var AllIncludes = AssetDatabase.LoadAssetAtPath<Shader>(ProtecctedFilePath).FindAllShaderIncludes();

                        File.Delete(ProtecctedFilePath);

                        foreach (var include in AllIncludes)
                        {
                            if (include.Contains("_Protected") || include.Contains("KannaModelDecode"))
                            {
                                File.Delete(include);
                            }
                        }
                    }

                    if (KannaProteccMaterial.IsShaderSupported(material.shader, out var shaderMatch) && shaderMatch.SupportsLocking && material.shader.name.Contains("Locked"))
                    {
                        Mats.Add(material);
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
            
            ShaderOptimizer.SetLockedForAllMaterials(Mats, 0, true, false, false);
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