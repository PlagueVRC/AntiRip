using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using Debug = UnityEngine.Debug;

namespace Kanna.Protecc
{
    [CustomEditor(typeof(KannaProteccRoot))]
    [CanEditMultipleObjects]
    public class KannaProteccRootEditor : Editor
    {
        SerializedProperty m_IgnoredMaterialsProperty;
        SerializedProperty m_AdditionalMaterialsProperty;
        SerializedProperty m_DistortRatioProperty;
        SerializedProperty m_KeysProperty;
        SerializedProperty m_VrcSavedParamsPathProperty;

        bool _debugFoldout = false;
        bool _lockKeys = true;

        ReorderableList m_IgnoreList;
        ReorderableList m_AdditionalList;

        SerializedProperty _pathProperty;
        SerializedProperty _disableObjectNameObfuscationProperty;
        SerializedProperty _excludeObjectNamesProperty;
        SerializedProperty _excludeParamNamesProperty;
        SerializedProperty _excludeAnimatorLayersProperty;
        ReorderableList _excludeObjectNamesPropertyList;
        ReorderableList _excludeParamNamesPropertyList;
        ReorderableList _excludeAnimatorLayersPropertyList;

        static bool _objectNameObfuscationFoldout = false;

        private bool IsVRCOpen;
        private bool EncryptedObjExists;

        Texture2D HeaderTexture;
        void OnEnable()
        {
            IsVRCOpen = Process.GetProcessesByName("VRChat").Length > 0;
            EncryptedObjExists = SceneManager.GetActiveScene().GetRootGameObjects().Any(o => o.name.Contains("_KannaProteccted"));

            m_DistortRatioProperty = serializedObject.FindProperty("_distortRatio");
            m_KeysProperty = serializedObject.FindProperty("_bitKeys");
            m_VrcSavedParamsPathProperty = serializedObject.FindProperty("_vrcSavedParamsPath");
            HeaderTexture = (Texture2D)AssetDatabase.LoadAssetAtPath($"{Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this))))}/Textures/Titlebar.png", typeof(Texture2D));
            m_AdditionalMaterialsProperty = serializedObject.FindProperty("m_AdditionalMaterials");
            m_IgnoredMaterialsProperty = serializedObject.FindProperty("m_IgnoredMaterials");

            m_AdditionalList = new ReorderableList(serializedObject, m_AdditionalMaterialsProperty, true, true, true, true)
            {
                drawElementCallback = AdditionalDrawListItems,
                drawHeaderCallback = AdditionalDrawHeader
            };

            m_IgnoreList = new ReorderableList(serializedObject, m_IgnoredMaterialsProperty, true, true, true, true)
            {
                drawElementCallback = IgnoreDrawListItems,
                drawHeaderCallback = IgnoreDrawHeader
            };

            _pathProperty = serializedObject.FindProperty("path");
            _disableObjectNameObfuscationProperty = serializedObject.FindProperty("disableObjectNameObfuscation");
            _excludeObjectNamesProperty = serializedObject.FindProperty("excludeObjectNames");
            _excludeParamNamesProperty = serializedObject.FindProperty("excludeParamNames");
            _excludeAnimatorLayersProperty = serializedObject.FindProperty("excludeAnimatorLayers");

            _excludeObjectNamesPropertyList = new ReorderableList(
                serializedObject,
                _excludeObjectNamesProperty,
                true,
                true,
                true,
                true
            );

            _excludeObjectNamesPropertyList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Exclude Objects From Renaming", EditorStyles.boldLabel);
            };

            _excludeObjectNamesPropertyList.drawElementCallback =
                (rect, index, isActive, isFocused) =>
                {
                    var element = _excludeObjectNamesPropertyList.serializedProperty.GetArrayElementAtIndex(index);
                    rect.y += 2;

                    EditorGUI.ObjectField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
                };

            _excludeParamNamesPropertyList = new ReorderableList(
                serializedObject,
                _excludeParamNamesProperty,
                true,
                true,
                true,
                true
            );

            _excludeParamNamesPropertyList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Exclude Parameters From Renaming", EditorStyles.boldLabel);
            };

            _excludeParamNamesPropertyList.drawElementCallback =
                (rect, index, isActive, isFocused) =>
                {
                    var element = _excludeParamNamesPropertyList.serializedProperty.GetArrayElementAtIndex(index);
                    rect.y += 2;

                    element.stringValue = EditorGUI.TextField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element.stringValue);
                };
            
            _excludeAnimatorLayersPropertyList = new ReorderableList(
                serializedObject,
                _excludeAnimatorLayersProperty,
                true,
                true,
                true,
                true
            );

            _excludeAnimatorLayersPropertyList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Exclude Animator Controllers From Obfuscation", EditorStyles.boldLabel);
            };

            _excludeAnimatorLayersPropertyList.drawElementCallback =
                (rect, index, isActive, isFocused) =>
                {
                   var element = _excludeAnimatorLayersPropertyList.serializedProperty.GetArrayElementAtIndex(index);
                    rect.y += 2;

                    var layer = (KannaProteccRoot.AnimLayerType)EditorGUI.EnumPopup(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                        //(Enum)Enum.ToObject(typeof(AnimLayerType), element.enumValueIndex), // Heavy object allocation
                        (KannaProteccRoot.AnimLayerType)element.intValue
                    );
                    element.intValue = (int)layer;
                };

            KannaProteccRoot.Instance = (KannaProteccRoot)target;
        }

        void AdditionalDrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, new GUIContent("Additional Materials (Such As Material Swaps)", "This lets you specify additional materials to have the Kanna Protecc code injected into when you click 'Protecc Avatar'. This will let you encrypt materials used in material swaps."));
        }

        void AdditionalDrawListItems(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = m_AdditionalList.serializedProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element);
        }

        void IgnoreDrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, new GUIContent("Ignored Materials", "These materials will be ignored by Kanna Protecc. If a mesh contains other materials that are not ignored it will still be encrypted."));
        }

        void IgnoreDrawListItems(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = m_IgnoreList.serializedProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var origColor = GUI.backgroundColor;

            GUILayout.Label("Universal Shader Support Branch");

            if (GUILayout.Button(new GUIContent(HeaderTexture, "Visit my Discord for help!"), EditorStyles.label, GUILayout.Height(Screen.width / 8f), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(999999f)))
            {
                Application.OpenURL("https://discord.gg/SyZcuTPXZA");
            }

            var KannaProteccRoot = target as KannaProteccRoot;
            
            //Do the big important buttons
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = !IsVRCOpen;

            if (KannaProteccRoot.IsProtected)
            {
                GUI.backgroundColor = Color.green;
            }

            if (GUILayout.Button(new GUIContent(!KannaProteccRoot.IsProtected ? (!IsVRCOpen ? "Protecc Avatar" : "Close VRChat To Encrypt") : "Un-Protecc Avatar", !KannaProteccRoot.IsProtected ? "Protecc's your avatar from rippers." : "Returns your avatar to its original form."), GUILayout.Height(Screen.width / 10f), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(999999f)))
            {
                if (KannaProteccRoot.IsProtected)
                {
                    UnProtecc(KannaProteccRoot);
                }
                else if (!IsVRCOpen)
                {
                    KannaProteccRoot.EncryptAvatar();
                }
            }

            if (KannaProteccRoot.IsProtected)
            {
                GUI.backgroundColor = origColor;
            }

            GUI.enabled = EncryptedObjExists && !IsVRCOpen;

            if (GUILayout.Button(new GUIContent(!IsVRCOpen ? "Write Keys" : "Close VRChat To Write Keys", "Write your keys to saved attributes!"), GUILayout.Height(Screen.width / 10f), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(999999f)))
            {
                KannaProteccRoot.WriteBitKeysToExpressions(GameObject.Find(KannaProteccRoot.gameObject.name.Trim() + "_KannaProteccted").GetComponent<VRCAvatarDescriptor>().expressionParameters, true, true);
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            //Do the properties
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.MaxWidth(999999f));
            m_DistortRatioProperty.floatValue = GUILayout.HorizontalSlider(m_DistortRatioProperty.floatValue, .6f, 5f);
            GUILayout.Space(15);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Encryption Intensity:");
            GUILayout.FlexibleSpace();
            m_DistortRatioProperty.floatValue = EditorGUILayout.FloatField(m_DistortRatioProperty.floatValue);
            GUILayout.EndHorizontal();
            GUILayout.Label("Set high enough so your encrypted mesh is visually wrecked, the higher the value, the more secure. Default = 5", EditorStyles.wordWrappedLabel);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.MaxWidth(999999f));
            GUILayout.Space(3);
            GUILayout.Label("VRC Saved Paramters Path");
            m_VrcSavedParamsPathProperty.stringValue = EditorGUILayout.TextField(m_VrcSavedParamsPathProperty.stringValue);
            GUILayout.Label("Ensure this is pointing to your LocalAvatarData folder!", EditorStyles.wordWrappedLabel);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);

            //draw additional and ignored material lists here
            GUILayout.Label(new GUIContent("Materials", "By default Kanna Protecc will inject its code into any Supported materials on this avatar. Here you can adjust that behaviour to include or remove some materials."), EditorStyles.boldLabel);
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                m_AdditionalList.DoLayoutList();
                if (GUILayout.Button(new GUIContent("Auto Detect", "Attempts to automatically detect additional materials, such as material swaps.")))
                {
                    var avatar = KannaProteccRoot.gameObject;

                    var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();

                    var Anims = new List<AnimationClip>();

                    Anims.AddRange(descriptor.baseAnimationLayers.Where(p => p.animatorController != null).SelectMany(o => o.animatorController.animationClips));
                    Anims.AddRange(descriptor.specialAnimationLayers.Where(p => p.animatorController != null).SelectMany(o => o.animatorController.animationClips));

                    var NativeMats = avatar.GetComponentsInChildren<Renderer>(true).SelectMany(o => o.sharedMaterials).ToArray();

                    foreach (var anim in Anims.Where(anim => anim != null))
                    {
                        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(anim);

                        Debug.Log($"Found Anim: {anim.name} With {bindings?.Length ?? 0} Bindings");

                        if (bindings != null && bindings.Length > 0)
                        {
                            foreach (var binding in bindings)
                            {
                                foreach (var frame in AnimationUtility.GetObjectReferenceCurve(anim, binding))
                                {
                                    var obj = frame.value;

                                    Debug.LogWarning($"Found Obj: {obj.GetType().Name}: {obj.name} In Anim: {anim.name}");

                                    if (obj is Material mat)
                                    {
                                        Debug.LogWarning($"Found Mat: {mat.name} In Anim: {anim.name}");

                                        if (!NativeMats.Contains(mat))
                                        {
                                            if (!KannaProteccRoot.Instance.m_AdditionalMaterials.Contains(mat))
                                            {
                                                KannaProteccRoot.Instance.m_AdditionalMaterials.Add(mat);

                                                Debug.Log($"Added Mat: {mat.name}");
                                            }
                                            else
                                            {
                                                Debug.Log($"Ignored Mat: {mat.name} - Already Added");
                                            }
                                        }
                                        else
                                        {
                                            Debug.Log($"Ignored Mat: {mat.name} - Not Additional");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                EditorGUILayout.Space();
                m_IgnoreList.DoLayoutList();
                EditorGUILayout.Space();
                EditorGUILayout.Separator();
            }
            GUILayout.Space(5f);

            //buttons for mats and key lock
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.MaxWidth(999999f));

            if (_lockKeys)
            {
                if (GUILayout.Button(new GUIContent("Unlock BitKeys", "Allow changes to key selections"))) _lockKeys = !_lockKeys;
            }
            else if (GUILayout.Button(new GUIContent("Lock BitKeys", "Prevent changes to key selections"))) _lockKeys = !_lockKeys;
            GUILayout.EndHorizontal();

            //draw keys here
            using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginDisabledGroup(_lockKeys);

                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("BitKeys", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("These are the keys used to encrypt the mesh.");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (_lockKeys)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Hidden To Prevent Accidentally Showing To Others - Unlock to show.");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }

                if (!_lockKeys)
                {
                    GUILayout.Space(5);
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginVertical();

                    //Display in 4 columns
                    for (var i = 0; i < m_KeysProperty.arraySize / 4; i++)
                    {
                        GUILayout.BeginHorizontal();
                        m_KeysProperty.GetArrayElementAtIndex(i).boolValue = GUILayout.Toggle(m_KeysProperty.GetArrayElementAtIndex(i).boolValue, ((KannaProteccRoot)target).GetBitKeyName(i, 7));
                        GUILayout.EndHorizontal();
                        GUILayout.Space(5f);
                    }

                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginVertical();
                    for (var i = m_KeysProperty.arraySize / 4; i < m_KeysProperty.arraySize / 2; i++)
                    {
                        GUILayout.BeginHorizontal();
                        m_KeysProperty.GetArrayElementAtIndex(i).boolValue = GUILayout.Toggle(m_KeysProperty.GetArrayElementAtIndex(i).boolValue, ((KannaProteccRoot)target).GetBitKeyName(i, 7));
                        GUILayout.EndHorizontal();
                        GUILayout.Space(5f);
                    }

                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginVertical();
                    for (var i = m_KeysProperty.arraySize / 2; i < (m_KeysProperty.arraySize / 4) * 3; i++)
                    {
                        GUILayout.BeginHorizontal();
                        m_KeysProperty.GetArrayElementAtIndex(i).boolValue = GUILayout.Toggle(m_KeysProperty.GetArrayElementAtIndex(i).boolValue, ((KannaProteccRoot)target).GetBitKeyName(i, 7));
                        GUILayout.EndHorizontal();
                        GUILayout.Space(5f);
                    }

                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginVertical();
                    for (var i = (m_KeysProperty.arraySize / 4) * 3; i < m_KeysProperty.arraySize; i++)
                    {
                        GUILayout.BeginHorizontal();
                        m_KeysProperty.GetArrayElementAtIndex(i).boolValue = GUILayout.Toggle(m_KeysProperty.GetArrayElementAtIndex(i).boolValue, ((KannaProteccRoot)target).GetBitKeyName(i, 7));
                        GUILayout.EndHorizontal();
                        GUILayout.Space(5f);
                    }

                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }

                //Generate key button
                EditorGUILayout.Space();
                if (GUILayout.Button(new GUIContent("Generate New Keys", "Generate new key overriding old one. Will need to write keys again!")))
                {
                    KannaProteccRoot.GenerateNewKey();
                }
                EditorGUI.EndDisabledGroup();
            }
            
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.Separator();
            _debugFoldout = EditorGUILayout.Foldout(_debugFoldout, "Debug And Fixing A Broken Avatar");
            if (_debugFoldout)
            {
                EditorGUILayout.Space();
                //GUILayout.BeginHorizontal();
                //GUILayout.FlexibleSpace();
                //if (GUILayout.Button(new GUIContent("Validate Animator Controller", "Validate all parameters, layers and animations are correct in this avatar's AnimatorController."), GUILayout.Height(Screen.width / 10), GUILayout.Width((Screen.width / 2) - 20f)))
                //{
                //    KannaProteccRoot.ValidateAnimatorController();
                //}

                if (GUILayout.Button(new GUIContent("Delete Kanna Protecc Objects From Controller", "Deletes all the objects Kanna Protecc wrote to your controller. Try running this if something gets weird with encrypting")))
                {
                    KannaProteccRoot.DeleteKannaProteccObjectsFromController();
                }
                //GUILayout.FlexibleSpace();
                //GUILayout.EndHorizontal();

                EditorGUILayout.Space();

                if (GUILayout.Button(new GUIContent("Force Un-Protecc", "Forces Un-Protecc in case of something going wrong.")))
                {
                    UnProtecc(KannaProteccRoot);
                }

                EditorGUILayout.Space();

                if (GUILayout.Button(new GUIContent("Create Test Log", "Ignore Pls Lol")))
                {
                    if (File.Exists(KannaProteccRoot.LogLocation))
                    {
                        File.Delete(KannaProteccRoot.LogLocation); // Remove Old Log
                    }

                    KannaLogger.LogToFile("Test Log 1", KannaProteccRoot.LogLocation);
                    KannaLogger.LogToFile("Test Log 2", KannaProteccRoot.LogLocation, KannaLogger.LogType.Warning);
                    KannaLogger.LogToFile("Test Log 3", KannaProteccRoot.LogLocation, KannaLogger.LogType.Error);
                }

                if (File.Exists(KannaProteccRoot.LogLocation))
                {
                    if (GUILayout.Button(new GUIContent("Open Latest Log", "Opens The Latest Kanna Protecc Log")))
                    {
                        Process.Start(KannaProteccRoot.LogLocation);
                    }
                }

                EditorGUILayout.Space();

                GUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("BitKeys Length:");

                var keycount = EditorGUILayout.IntField(((KannaProteccRoot)target)._bitKeys.Length);

                if (keycount != ((KannaProteccRoot)target)._bitKeys.Length) // Changed
                {
                    ((KannaProteccRoot)target)._bitKeys = new bool[keycount];
                    KannaProteccRoot.GenerateNewKey();
                }

                GUILayout.EndHorizontal();
            }
            //serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Obfuscator Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUI.enabled = true;

            if (_disableObjectNameObfuscationProperty != null)
            {
                var disableObjectNameToggle = !_disableObjectNameObfuscationProperty.boolValue;
                _objectNameObfuscationFoldout = !FeatureToggleFoldout(!_objectNameObfuscationFoldout, "Object Name Obfuscation", ref disableObjectNameToggle);

                if (!_objectNameObfuscationFoldout)
                {
                    _excludeObjectNamesPropertyList.DoLayoutList();
                }

                if (GUILayout.Button(new GUIContent("Auto-Exclude VRCFury Objects", "Tries to detect typical VRCFury objects and excludes them.")))
                {
                    var AllObjects = KannaProteccRoot.gameObject.GetComponentsInChildren<Transform>().Where(o => o.GetComponents<MonoBehaviour>().Any(p => p.GetType().Name.Contains("Fury")));

                    Debug.Log($"{string.Join(", ", AllObjects.Select(o => o.name))}");

                    foreach (var obj in AllObjects)
                    {
                        if (!KannaProteccRoot.excludeObjectNames.Contains(obj))
                        {
                            KannaProteccRoot.excludeObjectNames.Add(obj);
                        }
                    }
                }

                EditorGUILayout.Space();

                _disableObjectNameObfuscationProperty.boolValue = !disableObjectNameToggle;
            }

            FeatureToggleFoldout(true, "Parameter Name Obfuscation");

            _excludeParamNamesPropertyList.DoLayoutList();

            if (GUILayout.Button(new GUIContent("Auto Detect", "Attempts to automatically detect parameters for OSC for well known projects like VRCFT.")))
            {
                var expressions = KannaProteccRoot.gameObject.GetComponent<VRCAvatarDescriptor>().expressionParameters.parameters;

                foreach (var param in expressions)
                {
                    if (KannaProteccRoot.excludeParamNames.All(o => !Regex.IsMatch(param.name, o)))
                    {
                        if (Regex.IsMatch(param.name, @".*(FT\/|v2\/|Tracking).*")) // VRCFT
                        {
                            KannaProteccRoot.excludeParamNames.Add(@".*(FT\/|v2\/|Tracking).*");
                        }
                        else if (Regex.IsMatch(param.name, @".*(Go\/|(?i)go.*loco).*")) // VRCFT
                        {
                            KannaProteccRoot.excludeParamNames.Add(@".*(Go\/|(?i)go.*loco).*");
                        }
                        else if (Regex.IsMatch(param.name, @".*RealFeel.*")) // VRCFT
                        {
                            KannaProteccRoot.excludeParamNames.Add(@".*RealFeel.*");
                        }
                        else if (Regex.IsMatch(param.name, @"VFH\/.*")) // VRCFT
                        {
                            KannaProteccRoot.excludeParamNames.Add(@"VFH\/.*");
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            _excludeAnimatorLayersPropertyList.DoLayoutList();

            if (GUILayout.Button(new GUIContent("Auto Detect", "Attempts to automatically detect animators needing excluded, like GoGoLoco.")))
            {
                var descriptor = KannaProteccRoot.gameObject.GetComponent<VRCAvatarDescriptor>();

                var layers = descriptor.baseAnimationLayers.Concat(descriptor.specialAnimationLayers);

                foreach (var layer in layers)
                {
                    if (layer.animatorController == null || layer.animatorController.name == null || KannaProteccRoot.excludeAnimatorLayers.Contains((KannaProteccRoot.AnimLayerType)layer.type))
                    {
                        continue;
                    }

                    if (Regex.IsMatch(layer.animatorController.name, @".*(?i)go.*loco.*") || (layer.animatorController as AnimatorController).layers.Any(a => Regex.IsMatch(a.name, @".*(?i)go.*loco.*"))) // VRCFT
                    {
                        KannaProteccRoot.excludeAnimatorLayers.Add((KannaProteccRoot.AnimLayerType)layer.type);
                    }
                }
            }

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }

        void UnProtecc(KannaProteccRoot root)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                foreach (var obj in scene.GetRootGameObjects())
                {
                    if (obj != null && obj.name.StartsWith(root.gameObject.name) && (obj.name.EndsWith("_KannaProteccted") || obj.name.EndsWith("_Encrypted") || obj.name.EndsWith("_Encrypted_Obfuscated")))
                    {
                        DestroyImmediate(obj);
                    }
                }
            }

            root.gameObject.SetActive(true);

            MenuUtilites.UnlockAllMaterialsInHierarchy(null);

            root.DeleteKannaProteccObjectsFromController();

            ((KannaProteccRoot)target).obfuscator.ClearObfuscatedFiles((KannaProteccRoot)target);
        }

        static bool FeatureToggleFoldout(bool display, string title)
        {
            var style = new GUIStyle("ShurikenModuleTitle");
            style.font = new GUIStyle(EditorStyles.boldLabel).font;
            style.border = new RectOffset(15, 7, 4, 4);
            style.fixedHeight = 22;
            style.contentOffset = new Vector2(21f, -2f);

            var rect = GUILayoutUtility.GetRect(16f, 22f, style);
            GUI.Box(rect, title, style);
            var e = Event.current;

            var toggleRect = new Rect(rect.x + 21f, rect.y + 2f, 13f, 13f);

            var foldArrayRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
            if (e.type == EventType.Repaint)
            {
                EditorStyles.foldout.Draw(foldArrayRect, false, false, display, false);
            }
            else if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                display = !display;
                e.Use();
            }

            return display;
        }

        static bool FeatureToggleFoldout(bool display, string title, ref bool toggle)
        {
            var style = new GUIStyle("ShurikenModuleTitle");
            style.font = new GUIStyle(EditorStyles.boldLabel).font;
            style.border = new RectOffset(15, 7, 4, 4);
            style.fixedHeight = 22;
            style.contentOffset = new Vector2(41f, -2f);

            var rect = GUILayoutUtility.GetRect(16f, 22f, style);
            GUI.Box(rect, title, style);
            var e = Event.current;

            var toggleRect = new Rect(rect.x + 21f, rect.y + 2f, 13f, 13f);
            toggle = EditorGUI.Toggle(toggleRect, toggle);

            var foldArrayRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
            if (e.type == EventType.Repaint)
            {
                EditorStyles.foldout.Draw(foldArrayRect, false, false, display, false);
            }
            else if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                display = !display;
                e.Use();
            }

            return display;
        }
    }
}