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

    public static string GenerateRandomUniqueName(bool guid)
    {
        var str = "Kanna" + (guid ? $"_{GUID.Generate()}" : "");

        if (!guid)
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

        if (!(bool)Optimizer.GetMethod("SetLockedForAllMaterials", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { new[] { mat }, locked ? 1 : 0, /*ShowProgressBar*/true, /*Default Values, Reflection Needs All Defined:*/false, false, null }))
        {
            throw new Exception("Fuck. Thry Go WeeWoo.");
        }
    }

    public static void SetShadersLockedState(Material[] mats, bool locked)
    {
        if (!GetThry())
        {
            return;
        }

        if (!(bool)Optimizer.GetMethod("SetLockedForAllMaterials", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { mats, locked ? 1 : 0, /*ShowProgressBar*/true, /*Default Values, Reflection Needs All Defined:*/false, false, null }))
        {
            throw new Exception("Fuck. Thry Go WeeWoo.");
        }
    }

    public static void SetAllChildShadersLockedState(bool locked, params GameObject[] objects)
    {
        if (!GetThry())
        {
            return;
        }

        if (!(bool)Optimizer.GetMethod("SetLockForAllChildren", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { objects, locked ? 1 : 0, /*ShowProgressBar*/true, /*Default Values, Reflection Needs All Defined:*/false, false }))
        {
            throw new Exception("Fuck. Thry Go WeeWoo.");
        }
    }
}
#endif