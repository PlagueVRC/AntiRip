#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Kanna.Protecc;
using UnityEditor;
using UnityEngine;

public class Utilities
{
    private static int Length = 1;

    public static string GenerateRandomUniqueName(bool safeNaming, bool canHaveSpaces = false)
    {
        var str = $"Kanna{((safeNaming || canHaveSpaces) ? $"_{GUID.Generate()}" : "")}";

        if (!safeNaming)
        {
            str = str.PadRight(Length, '\u200B'); // \u200B = ZWSP

            Length++;
        }
        
        return str;
    }

    public static void ResetRandomizer()
    {
        Length = 1;
    }

    private static Type Optimizer;

    public static bool GetThry()
    {
        if (Optimizer == null)
        {
            Optimizer = KannaProteccRoot.GetTypeFromAnyAssembly("Thry.ShaderOptimizer");
        }
        else
        {
            return true;
        }

        if (Optimizer == null)
        {
            KannaLogger.LogToFile("Thry Is Not In Your Project, So Kanna Protecc Will Assume All Shaders Do Not Support Locking!", KannaProteccRoot.LogLocation, KannaLogger.LogType.Warning);
        }

        return Optimizer != null;
    }

    public static bool CanShaderBeLocked(Shader shader)
    {
        if (!GetThry())
        {
            return false;
        }

        return (bool)Optimizer.GetMethod("IsShaderUsingThryOptimizer", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { shader });
    }

    public static bool IsMaterialLocked(Material mat)
    {
        if (!GetThry())
        {
            return false;
        }

        return (bool)Optimizer.GetMethod("IsMaterialLocked", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { mat });
    }

    public static void SetShaderLockedState(Material mat, bool locked)
    {
        if (!GetThry())
        {
            return;
        }

        //var oldqueue = mat.renderQueue;
        //var oldrenderType = mat.GetTag("RenderType", false, "");
        if (!(bool)Optimizer.GetMethod("SetLockedForAllMaterials", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { new[] { mat }, locked ? 1 : 0, /*ShowProgressBar*/true, /*Default Values, Reflection Needs All Defined:*/false, false, null }))
        {
            throw new Exception("Fuck. Thry Go WeeWoo.");
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.Refresh();

        //mat.SetOverrideTag("RenderType", oldrenderType);
        //mat.renderQueue = oldqueue;

        //EditorUtility.SetDirty(mat);
        //AssetDatabase.Refresh();
    }

    public static void SetShadersLockedState(Material[] mats, bool locked)
    {
        if (!GetThry())
        {
            return;
        }

        //var olds = mats.Select(o => (o.renderQueue, o.GetTag("RenderType", false, ""))).ToArray(); // index will match

        if (!(bool)Optimizer.GetMethod("SetLockedForAllMaterials", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { mats, locked ? 1 : 0, /*ShowProgressBar*/true, /*Default Values, Reflection Needs All Defined:*/false, false, null }))
        {
            throw new Exception("Fuck. Thry Go WeeWoo.");
        }

        foreach (var mat in mats)
        {
            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.Refresh();

        //for (var index = 0; index < olds.Length; index++)
        //{
        //    var old = olds[index];
        //    mats[index].SetOverrideTag("RenderType", old.Item2);
        //    mats[index].renderQueue = old.renderQueue;

        //    EditorUtility.SetDirty(mats[index]);
        //}

        //AssetDatabase.Refresh();
    }
}
#endif