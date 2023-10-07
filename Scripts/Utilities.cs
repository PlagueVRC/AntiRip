#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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

        SaveChangeStack();
        Optimizer.GetMethod("SetLockedForAllMaterials", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { new[] { mat }, locked ? 1 : 0, /*ShowProgressBar*/true, /*Default Values, Reflection Needs All Defined:*/false, false, null });
        RestoreChangeStack();
    }

    public static void SetShadersLockedState(Material[] mats, bool locked)
    {
        if (!GetThry())
        {
            return;
        }

        SaveChangeStack();
        Optimizer.GetMethod("SetLockedForAllMaterials", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { mats, locked ? 1 : 0, /*ShowProgressBar*/true, /*Default Values, Reflection Needs All Defined:*/false, false, null });
        RestoreChangeStack();
    }

    //This code purly exists cause Unity 2019 is a piece of shit that looses it's internal change stack on locking CAUSE FUCK IF I KNOW
    static System.Reflection.FieldInfo changeStack = typeof(EditorGUI).GetField("s_ChangedStack", BindingFlags.Static | BindingFlags.NonPublic);
    static int preLockStackSize = 0;
    private static void SaveChangeStack()
    {
        if (changeStack != null)
        {
            Stack<bool> stack = (Stack<bool>)changeStack.GetValue(null);
            if (stack != null)
            {
                preLockStackSize = stack.Count;
            }
        }
    }

    private static void RestoreChangeStack()
    {
        if (changeStack != null)
        {
            Stack<bool> stack = (Stack<bool>)changeStack.GetValue(null);
            if (stack != null)
            {
                int postLockStackSize = stack.Count;
                //Restore change stack from before lock / unlocking
                for (int i = postLockStackSize; i < preLockStackSize; i++)
                {
                    EditorGUI.BeginChangeCheck();
                }
            }
        }
    }
}
#endif