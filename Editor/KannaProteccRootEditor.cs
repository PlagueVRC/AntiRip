using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Kanna.Protecc;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using Debug = UnityEngine.Debug;

namespace Kanna.Protecc
{
    [CustomEditor(typeof(KannaProteccRoot))]
    [CanEditMultipleObjects]
    public class KannaProteccRootEditor : UnityEditor.Editor
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        // Delegate for the EnumWindows callback function
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        
        [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        private static string GetText(IntPtr hWnd)
        {
            // Allocate correct string length first
            var length = GetWindowTextLength(hWnd);
            var sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        
        // Function to get all window handles - this is used later for checking in vrchat is open or closed, and if deepL local translation server is open
        private static List<IntPtr> GetAllWindowHandles()
        {
            var windowHandles = new List<IntPtr>();

            EnumWindows((IntPtr hWnd, IntPtr lParam) =>
            {
                // Add the handle to the list
                windowHandles.Add(hWnd);
                return true; // Continue enumeration
            }, IntPtr.Zero);

            return windowHandles;
        }
        
        SerializedProperty m_IgnoredMaterialsProperty;
        SerializedProperty m_AdditionalMaterialsProperty;
        SerializedProperty m_DistortRatioProperty;
        SerializedProperty m_KeysProperty;
        SerializedProperty m_VrcSavedParamsPathProperty;

        bool _debugFoldout;
        bool _lockKeys = true;
        private bool _isavacrypt;
        private bool _ismissingessentials;
        private Material[] NativeMats;

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

        static bool _objectNameObfuscationFoldout;

        private bool IsVRCOpen;
        private bool EncryptedObjExists;
        private bool IsDeepLFreeAPIOpen;
        private string[] Languages;

        Texture2D HeaderTexture;

        private string AntiRipFolder;

        private static List<RuntimeAnimatorController> AllControllers;

        void OnEnable()
        {
            KannaProteccRoot.Instance = (KannaProteccRoot)target;
            
            var obj = KannaProteccRoot.Instance.gameObject;

            var descriptor = obj.GetComponent<VRCAvatarDescriptor>();

            var MainAnimator = obj.GetComponent<Animator>().runtimeAnimatorController;
            
            NativeMats = obj.GetComponentsInChildren<Renderer>(true).SelectMany(o => o.sharedMaterials).ToArray();

            AllControllers = obj.GetComponentsInChildren<Animator>(true).Select(o => o.runtimeAnimatorController).Where(a => a != null).Concat(descriptor.baseAnimationLayers.Select(p => p.animatorController)).Concat(descriptor.specialAnimationLayers.Select(p => p.animatorController)).ToList();

            _ismissingessentials = MainAnimator == null || descriptor.baseAnimationLayers.First(o => o.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController == null || descriptor.expressionParameters == null || descriptor.expressionsMenu == null;
            
            _isavacrypt = AllControllers.Any(o => o != null && ((AnimatorController)o) is var Generic && ( Generic.layers.Any(p => p.name.StartsWith("BitKey")) || Generic.parameters.Any(i => i.name.StartsWith("BitKey")) || Generic.animationClips.Any(u => u.name.Contains("_BitKey_")) || descriptor.expressionParameters.parameters.Any(y => y.name.StartsWith("BitKey")) ) );

            AntiRipFolder = Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this))));

            Languages = typeof(Translator.Languages).GetFields(BindingFlags.NonPublic | BindingFlags.Static).Select(x => x.Name).ToArray();

            if (KannaProteccRoot.Instance.SelectedLanguage == -1)
            {
                var culture = CultureInfo.CurrentUICulture.Name.ToLower();
                var withoutdash = culture.Substring(0, culture.IndexOf("-"));

                var lang = Languages.ToList().FindIndex(o => culture == o || withoutdash == o);

                KannaProteccRoot.Instance.SelectedLanguage = lang == -1 ? (Languages.Length - 1) : lang; // Last Is English
            }

            // This wont work, same reason I had to adjust IsVRCOpen to Windows API. Sigh.
            // Luckily, I shouldn't need to use this for a long while, such as if DeepL adds a new language.
            // Thus, later me can deal with getting this bullshit to work in windows API.
            #if UNITY_2022
            var handles = Process.GetProcesses(); //GetAllWindowHandles();
            IsDeepLFreeAPIOpen = handles.Any(o => GetText(o.MainWindowHandle) is var text && text.StartsWith("DeepL Translate: The"));

            if (IsDeepLFreeAPIOpen)
            {
                Debug.Log("DeepL Free API Found.");
            }

            IsVRCOpen = handles.Any(o => GetText(o.MainWindowHandle) is var text && (text == "VRChat" || (text.Contains("VRChat") && text.Contains("Beta"))));
            
            if (IsVRCOpen)
            {
                Debug.Log("VRChat Found.");
            }
            #elif UNITY_2019
            var handles = GetAllWindowHandles();
            IsDeepLFreeAPIOpen = handles.Any(o => GetText(o) is var text && text.StartsWith("DeepL Translate: The"));

            if (IsDeepLFreeAPIOpen)
            {
                Debug.Log("DeepL Free API Found.");
            }

            IsVRCOpen = handles.Any(o => GetText(o) is var text && (text == "VRChat" || (text.Contains("VRChat") && text.Contains("Beta"))));
            
            if (IsVRCOpen)
            {
                Debug.Log("VRChat Found.");
            }
            #endif
            
            EncryptedObjExists = SceneManager.GetActiveScene().GetRootGameObjects().Any(o => o.name.Contains("_KannaProteccted"));

            m_DistortRatioProperty = serializedObject.FindProperty("_distortRatio");
            m_KeysProperty = serializedObject.FindProperty("_bitKeys");
            m_VrcSavedParamsPathProperty = serializedObject.FindProperty("_vrcSavedParamsPath");
            HeaderTexture = (Texture2D)AssetDatabase.LoadAssetAtPath($"{AntiRipFolder}/Textures/Titlebar.png", typeof(Texture2D));
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
                EditorGUI.LabelField(rect, KannaProteccRoot.Instance.ExcludeObjectsLabel_Localized, EditorStyles.boldLabel);
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
                EditorGUI.LabelField(rect, KannaProteccRoot.Instance.ExcludeParamsLabel_Localized, EditorStyles.boldLabel);
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
                EditorGUI.LabelField(rect, KannaProteccRoot.Instance.ExcludeAnimsLabel_Localized, EditorStyles.boldLabel);
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

        }

        void AdditionalDrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, new GUIContent(KannaProteccRoot.Instance.AdditionalMaterials_Localized, KannaProteccRoot.Instance.AdditionalMaterialsTooltip_Localized));
        }

        void AdditionalDrawListItems(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = m_AdditionalList.serializedProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element);
        }

        void IgnoreDrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, new GUIContent(KannaProteccRoot.Instance.IgnoredMaterials_Localized, KannaProteccRoot.Instance.IgnoredMaterialsTooltip_Localized));
        }

        void IgnoreDrawListItems(Rect rect, int index, bool isActive, bool isFocused)
        {
            var element = m_IgnoreList.serializedProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element);
        }

        public override async void OnInspectorGUI() // I sure hope the async add doesn't come back to bite me and unity handles things for me...
        {
            serializedObject.Update();

            var wrappedlabel = EditorStyles.wordWrappedLabel;
            var boldwrappedlabel = EditorStyles.boldLabel;
            boldwrappedlabel.wordWrap = true;

            var origColor = GUI.backgroundColor;

            var KannaProteccRoot = target as KannaProteccRoot;

            //IsVRCOpen = false; //GetAllWindowHandles().Any(o => GetText(o) is var text && (text == "VRChat" || (text.Contains("VRChat") && text.Contains("Beta"))));

            if (GUILayout.Button(new GUIContent(HeaderTexture, KannaProteccRoot.DiscordMessage_Localized), EditorStyles.label, GUILayout.Height(Screen.width / 8f), GUILayout.ExpandWidth(true)))
            {
                Application.OpenURL("https://discord.gg/SyZcuTPXZA");
            }

            if (_ismissingessentials)
            {
                GUI.color = Color.red;
                GUILayout.Label(KannaProteccRoot.MissingEssentialsLabel_Localized, boldwrappedlabel);
                
                var obj = KannaProteccRoot.Instance.gameObject;

                var descriptor = obj.GetComponent<VRCAvatarDescriptor>();

                var MainAnimator = obj.GetComponent<Animator>().runtimeAnimatorController;
                
// MainAnimator == null || descriptor.baseAnimationLayers.First(o => o.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController == null || descriptor.expressionParameters == null || descriptor.expressionsMenu == null
                if (MainAnimator == null)
                {
                    // animator slot empty
                    GUI.color = Color.white;
                    if (GUILayout.Button("Fix"))
                    {
                        obj.GetComponent<Animator>().runtimeAnimatorController = descriptor.baseAnimationLayers.First(o => o.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
                        EditorUtility.SetDirty(obj.GetComponent<Animator>());
                        OnEnable();
                    }
                }
                
                return;
            }

            if (_isavacrypt)
            {
                GUI.color = Color.red;
                GUILayout.Label(KannaProteccRoot.LingeringAvaCryptLabel_Localized, boldwrappedlabel);
                GUI.color = Color.white;
                if (GUILayout.Button(new GUIContent(KannaProteccRoot.AutoFixLabel_Localized, KannaProteccRoot.AutoFixLingeringAvaCryptTooltip_Localized)))
                {
                    foreach (var runtimeAnimatorController in AllControllers)
                    {
                        var controller = (AnimatorController)runtimeAnimatorController;
                        
                        var layers = controller.layers.ToList();
                        layers.RemoveAll(o => o.name.StartsWith("BitKey"));
                        controller.layers = layers.ToArray();
                        
                        var parameters = controller.parameters.ToList();
                        parameters.RemoveAll(o => o.name.StartsWith("BitKey"));
                        controller.parameters = parameters.ToArray();
                    }

                    var descriptor = KannaProteccRoot.gameObject.GetComponent<VRCAvatarDescriptor>();
                    
                    var eparameters = descriptor.expressionParameters.parameters.ToList();
                    eparameters.RemoveAll(o => o.name.StartsWith("BitKey"));
                    descriptor.expressionParameters.parameters = eparameters.ToArray();
                    
                    _isavacrypt = AllControllers.Any(o => o != null && ((AnimatorController)o) is var Generic && ( Generic.layers.Any(p => p.name.StartsWith("BitKey")) || Generic.parameters.Any(i => i.name.StartsWith("BitKey")) || Generic.animationClips.Any(u => u.name.Contains("_BitKey_")) || descriptor.expressionParameters.parameters.Any(y => y.name.StartsWith("BitKey")) ) );
                }
                return;
            }

            if (IsDeepLFreeAPIOpen)
            {
                var NewLanguage = EditorGUILayout.Popup(KannaProteccRoot.UILanguage_Localized, KannaProteccRoot.SelectedLanguage, Languages);
                if (NewLanguage != KannaProteccRoot.SelectedLanguage)
                {
                    KannaProteccRoot.SelectedLanguage = NewLanguage;

                    TranslateUI();
                }
            }
            else if (File.Exists($"{AntiRipFolder}\\Localization.json"))
            {
                var NewLanguage = EditorGUILayout.Popup(KannaProteccRoot.UILanguage_Localized, KannaProteccRoot.SelectedLanguage, Languages);
                if (NewLanguage != KannaProteccRoot.SelectedLanguage)
                {
                    KannaProteccRoot.SelectedLanguage = NewLanguage;

                    var SelectedLang = typeof(Translator.Languages).GetField(Languages[KannaProteccRoot.SelectedLanguage], BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null).ToString();

                    var localizations = JsonConvert.DeserializeObject<Localizations>(File.ReadAllText($"{AntiRipFolder}\\Localization.json")).Translations[SelectedLang ?? "en"];

                    foreach (var localization in localizations)
                    {
                        typeof(KannaProteccRoot).GetField(localization.FieldName, BindingFlags.Public | BindingFlags.Instance).SetValue(KannaProteccRoot, localization.FieldValue);
                    }
                }
            }
            else
            {
                if (GUILayout.Button(new GUIContent("UI Language Translator Missing! Hover For Details!", "DeepLFreeAPI Not Found! Install For Localizing! Click To Open GitHub Link To DeepLFreeAPI!"), GUILayout.Height(Screen.width / 10f), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(999999f)))
                {
                    Process.Start("https://github.com/MistressPlague/DeepLFreeLocalAPI");
                }
            }

            //Do the big important buttons
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = !IsVRCOpen;

            if (KannaProteccRoot.IsProtected)
            {
                GUI.backgroundColor = Color.green;
            }

            if (GUILayout.Button(new GUIContent(!KannaProteccRoot.IsProtected ? (!IsVRCOpen ? KannaProteccRoot.ProteccAvatar_Localized : KannaProteccRoot.CloseVRCToEncrypt_Localized) : KannaProteccRoot.UnproteccAvatar_Localized, !KannaProteccRoot.IsProtected ? KannaProteccRoot.ProteccFromRippersTooltip_Localized : KannaProteccRoot.OriginalFormTooltip_Localized), GUILayout.Height(Screen.width / 10f), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(999999f)))
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

            if (GUILayout.Button(new GUIContent(!IsVRCOpen ? KannaProteccRoot.WriteKeys_Localized : KannaProteccRoot.CloseVRChatToWriteKeys_Localized, KannaProteccRoot.WriteKeysTooltip_Localized), GUILayout.Height(Screen.width / 10f), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(999999f)))
            {
                KannaProteccRoot.WriteBitKeysToExpressions(GameObject.Find(KannaProteccRoot.gameObject.name + "_KannaProteccted").GetComponent<VRCAvatarDescriptor>().expressionParameters, true, true);
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
            GUILayout.Label(KannaProteccRoot.EncryptionIntensityLabel_Localized, wrappedlabel);
            GUILayout.FlexibleSpace();
            m_DistortRatioProperty.floatValue = EditorGUILayout.FloatField(m_DistortRatioProperty.floatValue);
            GUILayout.EndHorizontal();
            GUILayout.Label(KannaProteccRoot.EncryptionIntensityInfoLabel_Localized, wrappedlabel);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.MaxWidth(999999f));
            GUILayout.Space(3);
            GUILayout.Label(KannaProteccRoot.VRCSavedParamtersPathLabel_Localized, wrappedlabel);
            m_VrcSavedParamsPathProperty.stringValue = EditorGUILayout.TextField(m_VrcSavedParamsPathProperty.stringValue);
            GUILayout.Label(KannaProteccRoot.EnsureLocalAvatarPathLabel_Localized, wrappedlabel);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);

            //draw additional and ignored material lists here
            GUILayout.Label(new GUIContent(KannaProteccRoot.MaterialsTooltip_Localized, KannaProteccRoot.MaterialsTooltip_Localized), boldwrappedlabel);
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                m_AdditionalList.DoLayoutList();
                if (KannaProteccRoot.m_AdditionalMaterials.Any(o => o != null && NativeMats.Contains(o)))
                {
                    KannaProteccRoot.m_AdditionalMaterials = KannaProteccRoot.m_AdditionalMaterials.Where(p => p != null && !NativeMats.Contains(p)).ToList(); // Repair User Error
                }
                
                if (GUILayout.Button(new GUIContent(KannaProteccRoot.AutoDetect_Localized, KannaProteccRoot.AutoDetectMaterialsTooltip_Localized)))
                {
                    var avatar = KannaProteccRoot.gameObject;

                    var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();

                    var Anims = new List<AnimationClip>();

                    Anims.AddRange(descriptor.baseAnimationLayers.Where(p => p.animatorController != null).SelectMany(o => o.animatorController.animationClips));
                    Anims.AddRange(descriptor.specialAnimationLayers.Where(p => p.animatorController != null).SelectMany(o => o.animatorController.animationClips));

                    NativeMats = avatar.GetComponentsInChildren<Renderer>(true).SelectMany(o => o.sharedMaterials).ToArray();

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
                if (GUILayout.Button(new GUIContent(KannaProteccRoot.UnlockBitKeys_Localized, KannaProteccRoot.UnlockBitKeysTooltip_Localized))) _lockKeys = !_lockKeys;
            }
            else if (GUILayout.Button(new GUIContent(KannaProteccRoot.LockBitKeys_Localized, KannaProteccRoot.LockBitKeysTooltip_Localized))) _lockKeys = !_lockKeys;
            GUILayout.EndHorizontal();

            //draw keys here
            using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginDisabledGroup(_lockKeys);

                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(KannaProteccRoot.BitKeysLabel_Localized, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(KannaProteccRoot.EncryptTheMeshLabel_Localized);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (_lockKeys)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(KannaProteccRoot.HiddenToPreventLabel_Localized);
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
                if (GUILayout.Button(new GUIContent(KannaProteccRoot.GenerateNewKeys_Localized, KannaProteccRoot.GenerateNewKeysTooltip_Localized)))
                {
                    KannaProteccRoot.GenerateNewKey();
                }
                EditorGUI.EndDisabledGroup();
            }
            
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.Separator();
            _debugFoldout = EditorGUILayout.Foldout(_debugFoldout, KannaProteccRoot.DebugAndFix_Localized);
            if (_debugFoldout)
            {
                EditorGUILayout.Space();
                //GUILayout.BeginHorizontal();
                //GUILayout.FlexibleSpace();
                //if (GUILayout.Button(new GUIContent("Validate Animator Controller", "Validate all parameters, layers and animations are correct in this avatar's AnimatorController."), GUILayout.Height(Screen.width / 10), GUILayout.Width((Screen.width / 2) - 20f)))
                //{
                //    KannaProteccRoot.ValidateAnimatorController();
                //}

                if (GUILayout.Button(new GUIContent(KannaProteccRoot.DeleteKannaProteccObjects_Localized, KannaProteccRoot.DeleteKannaProteccObjectsTooltip_Localized)))
                {
                    KannaProteccRoot.DeleteKannaProteccObjectsFromController();
                }
                //GUILayout.FlexibleSpace();
                //GUILayout.EndHorizontal();

                EditorGUILayout.Space();

                if (GUILayout.Button(new GUIContent(KannaProteccRoot.ForceUnprotecc_Localized, KannaProteccRoot.ForceUnprotecTooltip_Localized)))
                {
                    UnProtecc(KannaProteccRoot);
                }

                EditorGUILayout.Space();

                if (GUILayout.Button(new GUIContent(KannaProteccRoot.CreateTestLog_Localized, KannaProteccRoot.CreateTestLogTooltip_Localized)))
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
                    if (GUILayout.Button(new GUIContent(KannaProteccRoot.OpenLatestLog_Localized, KannaProteccRoot.OpenLatestLogTooltip_Localized)))
                    {
                        Process.Start(KannaProteccRoot.LogLocation);
                    }
                }

                EditorGUILayout.Space();

                GUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(KannaProteccRoot.BitKeysLengthLabelField_Localized);

                var keycount = EditorGUILayout.IntField(((KannaProteccRoot)target)._bitKeys.Length);

                if (keycount != ((KannaProteccRoot)target)._bitKeys.Length) // Changed
                {
                    ((KannaProteccRoot)target)._bitKeys = new bool[keycount];
                    KannaProteccRoot.GenerateNewKey();
                }

                GUILayout.EndHorizontal();

                if (Environment.UserName == "krewe" && KannaProteccRoot.SelectedLanguage != -1) // Dev
                {
                    void SaveTranslations(int SelLang = -1)
                    {
                        if (SelLang == -1)
                        {
                            SelLang = KannaProteccRoot.SelectedLanguage;
                        }
                        
                        var SelectedLang = typeof(Translator.Languages).GetField(Languages[SelLang], BindingFlags.NonPublic | BindingFlags.Static).GetValue(null).ToString();

                        var AllFields = typeof(KannaProteccRoot).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(o => o.Name.EndsWith("_Localized"));

                        var Localizations = new List<Localization>();

                        foreach (var field in AllFields)
                        {
                            Localizations.Add(new Localization
                            {
                                FieldName = field.Name,
                                FieldValue = field.GetValue(KannaProteccRoot).ToString()
                            });
                        }

                        var AllLangLocalizations = File.Exists("Assets\\AntiRip\\Localization.json") ? JsonConvert.DeserializeObject<Localizations>(File.ReadAllText("Assets\\AntiRip\\Localization.json")) : new Localizations();

                        if (AllLangLocalizations.Translations.ContainsKey(SelectedLang))
                        {
                            var localizations = new List<Localization>();
                            
                            localizations.AddRange(Localizations);

                            for (var index = 0; index < localizations.Count; index++)
                            {
                                var entry = localizations[index];
                                
                                var match = AllLangLocalizations.Translations[SelectedLang].FirstOrDefault(o => o.FieldName == entry.FieldName);

                                if (match != null)
                                {
                                    localizations[index] = match;
                                }
                                else
                                {
                                    Debug.Log($"Added: {entry.FieldName}");
                                }
                            }

                            AllLangLocalizations.Translations[SelectedLang] = localizations;
                        }
                        else
                        {
                            AllLangLocalizations.Translations[SelectedLang] = Localizations;
                        }
                        
                        File.WriteAllText("Assets\\AntiRip\\Localization.json", JsonConvert.SerializeObject(AllLangLocalizations, Formatting.Indented));

                        Debug.Log("Done Writing!");
                    }
                    
                    if (IsDeepLFreeAPIOpen)
                    {
                        if (GUILayout.Button(new GUIContent("Update Translations", "Updates All Translations When New Fields Are Added.")))
                        {
                            for (var index = 0; index < Languages.Length; index++)
                            {
                                var Lang = Languages[index];

                                await TranslateUI(index);
                                
                                SaveTranslations(index);

                                await TranslateUI(Languages.Length - 1);
                                
                                Debug.Log($"Finished: {Lang}");
                            }
                        }
                    }
                    
                    if (GUILayout.Button(new GUIContent("Save Translations To JSON", "Saves Translations To JSON For Publishing")))
                    {
                        SaveTranslations();
                    }
                }
            }
            //serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField(KannaProteccRoot.ObfuscatorSettingsLabelField_Localized, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUI.enabled = true;

            if (_disableObjectNameObfuscationProperty != null)
            {
                var disableObjectNameToggle = !_disableObjectNameObfuscationProperty.boolValue;
                _objectNameObfuscationFoldout = !FeatureToggleFoldout(!_objectNameObfuscationFoldout, KannaProteccRoot.ObjectNameObfuscation_Localized, ref disableObjectNameToggle);

                if (!_objectNameObfuscationFoldout)
                {
                    _excludeObjectNamesPropertyList.DoLayoutList();
                }

                if (GUILayout.Button(new GUIContent(KannaProteccRoot.AutoExcludeVRCFuryObjects_Localized, KannaProteccRoot.AutoExcludeVRCFuryObjectsTooltip_Localized)))
                {
                    var AllObjects = KannaProteccRoot.gameObject.GetComponentsInChildren<Transform>().Where(o => o != KannaProteccRoot.transform && o.GetComponents<MonoBehaviour>().Any(p => p.GetType().Name.Contains("Fury")));

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

            FeatureToggleFoldout(true, KannaProteccRoot.ParameterNameObfuscationRegEx_Localized);

            _excludeParamNamesPropertyList.DoLayoutList();

            if (GUILayout.Button(new GUIContent(KannaProteccRoot.AutoDetect_Localized, KannaProteccRoot.AutoDetectParamsTooltip_Localized)))
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
                        else if (Regex.IsMatch(param.name, @".*(Go\/|(?i)go.*loco).*")) // Gogo Loco
                        {
                            KannaProteccRoot.excludeParamNames.Add(@".*(Go\/|(?i)go.*loco).*");
                        }
                        else if (Regex.IsMatch(param.name, @".*RealFeel.*")) // RealFeel
                        {
                            KannaProteccRoot.excludeParamNames.Add(@".*RealFeel.*");
                        }
                        else if (Regex.IsMatch(param.name, @"VFH\/.*")) // VFH
                        {
                            KannaProteccRoot.excludeParamNames.Add(@"VFH\/.*");
                        }
                        else if (Regex.IsMatch(param.name, @".*OSC.*")) // OSC
                        {
                            KannaProteccRoot.excludeParamNames.Add(@".*OSC.*");
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            _excludeAnimatorLayersPropertyList.DoLayoutList();

            if (GUILayout.Button(new GUIContent(KannaProteccRoot.AutoDetect_Localized, KannaProteccRoot.AutoDetectAnimatorsTooltip_Localized)))
            {
                var descriptor = KannaProteccRoot.gameObject.GetComponent<VRCAvatarDescriptor>();

                var layers = descriptor.baseAnimationLayers.Concat(descriptor.specialAnimationLayers);

                foreach (var layer in layers)
                {
                    if (layer.animatorController == null || layer.animatorController.name == null || KannaProteccRoot.excludeAnimatorLayers.Contains((KannaProteccRoot.AnimLayerType)layer.type))
                    {
                        continue;
                    }

                    if (Regex.IsMatch(layer.animatorController.name, @".*(?i)go.*loco.*") || (layer.animatorController as AnimatorController).layers.Any(a => Regex.IsMatch(a.name, @".*(?i)go.*loco.*")) || Regex.IsMatch(layer.animatorController.name, @".*(?i)e tracking.*") || Regex.IsMatch(layer.animatorController.name, @".*(?i)vrcft.*") || (layer.animatorController as AnimatorController).layers.Any(a => Regex.IsMatch(a.name, @".*(?i)e_tracking_.*"))) // VRCFT
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

        public class Localizations
        {
            public Dictionary<string, List<Localization>> Translations = new Dictionary<string, List<Localization>>();
        }

        public class Localization
        {
            public string FieldName;
            public string FieldValue;
        }

        async Task TranslateUI(int SelectedLanguage = -1)
        {
            var _Instance = KannaProteccRoot.Instance;

            if (SelectedLanguage == -1)
            {
                SelectedLanguage = _Instance.SelectedLanguage;
            }
            
            if (SelectedLanguage == -1)
            {
                return;
            }

            var Lang = typeof(Translator.Languages).GetField(Languages[SelectedLanguage], BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null).ToString();

            var translated = new List<string>();
            
            if (File.Exists($"{AntiRipFolder}\\Localization.json"))
            {
                var localizations = JsonConvert.DeserializeObject<Localizations>(File.ReadAllText($"{AntiRipFolder}\\Localization.json")).Translations[Lang ?? "en"];

                foreach (var localization in localizations)
                {
                    typeof(KannaProteccRoot).GetField(localization.FieldName, BindingFlags.Public | BindingFlags.Instance).SetValue(_Instance, localization.FieldValue);
                    translated.Add(localization.FieldName);
                }
            }
            
            Debug.Log(Lang);

            foreach (var field in typeof(KannaProteccRoot).GetFields().Where(o => !translated.Contains(o.Name) && o.Name.EndsWith("_Localized") && o.FieldType == typeof(string)))
            {
                field.SetValue(_Instance, Lang != Translator.Languages.English ? (await Translator.TranslateText((string)field.GetValue(_Instance), Lang)).translated_text : (string)field.GetValue(_Instance));
            }

            Debug.Log("Done Translating!");
        }

    }
}