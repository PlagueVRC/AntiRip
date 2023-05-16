using System.Collections.Generic;
using System.Linq;
using Thry;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace GeoTetra.GTAvaUtil
{
    public class MenuUtilites
    {
        const string okText = "Ok";
        
        [MenuItem("Tools/GeoTetra/GTAvaCrypt/Unlock All Poi Materials In Hierarchy...", false)]
        public static void UnlockAllPoiMaterialsInHierarchy(MenuCommand command)
        {
            const string message = "Select a Root GameObject which has children with locked Poiyomi 8 materials.";
            
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

            GameObject gameObject = (Selection.objects[0] as GameObject);
            
            if (gameObject == null)
            {
                ErrorDialogue();
                return;
            }

            SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            MeshRenderer[] renderers = gameObject.GetComponentsInChildren<MeshRenderer>();

            if (skinnedMeshRenderers.Length == 0 && renderers.Length == 0)
            {
                EditorUtility.DisplayDialog("No SkinnedMeshRenderers nor MeshRenderers found!",
                    message,
                    okText);
                return;
            }

            List<Material> poiMats = new List<Material>();
            
            foreach (var renderer in skinnedMeshRenderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader.name.Contains("Hidden/Locked/.poiyomi/Poiyomi"))
                    {
                        poiMats.Add(material);
                    }
                }
            }
            
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader.name.Contains("Hidden/Locked/.poiyomi/Poiyomi"))
                    {
                        poiMats.Add(material);
                    }
                }
            }
            
            if (poiMats.Count == 0)
            {
                EditorUtility.DisplayDialog("All Poiyomi 8 materials already unlocked in hiearchy!",
                    message,
                    okText);
                return;
            }
            
            ShaderOptimizer.SetLockedForAllMaterials(poiMats, 0, true, false, false);
        }
        
        [MenuItem("Tools/GeoTetra/GTAvaCrypt/Check for Update...", false)]
        static void CheckForUpdate()
        {
            var list = UnityEditor.PackageManager.Client.List();
            while (!list.IsCompleted)
            { }
            PackageInfo package = list.Result.FirstOrDefault(q => q.name == "com.geotetra.gtavacrypt");
            if (package == null)
            {
                EditorUtility.DisplayDialog("Not installed via UPM!",
                    "This upgrade option only works if you installed via UPM. Go to AvaCrypt github and reinstall via UPM if you wish to use this",
                    okText);
                return;
            }

            UnityEditor.PackageManager.Client.Add("https://github.com/rygo6/GTAvaCrypt.git");
        }
    }
}