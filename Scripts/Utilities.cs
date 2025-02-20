#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Kanna.Protecc;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class Utilities
{
    private static int Length = 1;

    public static string GenerateRandomUniqueName(bool safeNaming)
    {
        var str = $"Kanna{((safeNaming) ? $"_{GUID.Generate()}" : "")}";

        if (!safeNaming)
        {
            // \u200B = ZWSP
            str += new string('\u200B', Length);

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
    
    /// <summary>
    /// Recursively deep copies all fields from source to target.
    /// </summary>
    public static void DeepCopyFields(object source, object target)
    {
        if (source == null || target == null)
            throw new ArgumentNullException("Source or Target object is null!");

        var fields = source.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            if (field.IsStatic || field.IsInitOnly) continue;

            var fieldValue = field.GetValue(source);

            // Deep clone the field value
            var clonedValue = DeepCloneValue(fieldValue);

            field.SetValue(target, clonedValue);
        }
    }

    /// <summary>
    /// Deep copies all properties from source to target.
    /// </summary>
    public static void DeepCopyProperties(object source, object target)
    {
        if (source == null || target == null)
            throw new ArgumentNullException("Source or Target object is null!");

        var properties = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var property in properties)
        {
            if (!property.CanRead || !property.CanWrite) continue; // Ensure property has both getter and setter
            if (property.GetIndexParameters().Length > 0) continue; // Skip indexed properties

            try
            {
                var propertyValue = property.GetValue(source);

                // Deep clone the property value
                var clonedValue = DeepCloneValue(propertyValue);

                property.SetValue(target, clonedValue);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to copy property '{property.Name}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Deeply clones a value, recursively duplicating any nested or referenced objects.
    /// </summary>
    private static object DeepCloneValue(object val)
    {
        if (val == null)
            return null;

        var type = val.GetType();

        // Handle arrays (deep copy each element)
        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            var sourceArray = (Array)val;
            var clonedArray = Array.CreateInstance(elementType, sourceArray.Length);

            for (var i = 0; i < sourceArray.Length; i++)
            {
                clonedArray.SetValue(DeepCloneValue(sourceArray.GetValue(i)), i);
            }

            return clonedArray;
        }
        
        // Handle lists/generic collections (deep copy each element)
        if (val is IList list)
        {
            var listType = val.GetType();
            var clonedList = (IList)Activator.CreateInstance(listType);

            foreach (var element in list)
            {
                clonedList.Add(DeepCloneValue(element));
            }

            return clonedList;
        }

        // Handle dictionaries (deep copy keys and values)
        if (val is IDictionary dictionary)
        {
            var dictionaryType = val.GetType();
            var clonedDictionary = (IDictionary)Activator.CreateInstance(dictionaryType);

            foreach (DictionaryEntry entry in dictionary)
            {
                var clonedKey = DeepCloneValue(entry.Key);
                var clonedValue = DeepCloneValue(entry.Value);
                clonedDictionary.Add(clonedKey, clonedValue);
            }

            return clonedDictionary;
        }

        // Handle simple value types and strings (return directly)
        if (type.IsValueType || val is string)
            return val;

        if (val is Object obj)
        {
            // Handle complex objects (create a new instance and recursively copy fields)
            var clonedObject = Object.Instantiate(obj);
            DeepCopyFields(val, clonedObject);
            DeepCopyProperties(val, clonedObject); // Recursively handle properties too
            return clonedObject;
        }

        return val;
    }

}
#endif
