using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

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
        ReorderableList _excludeObjectNamesPropertyList;
        ReorderableList _excludeParamNamesPropertyList;

        static bool _objectNameObfuscationFoldout = false;

        Texture2D HeaderTexture;
        void OnEnable()
        {
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

            //

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

            KannaProteccRoot.Instance = (KannaProteccRoot)target;
        }

        void AdditionalDrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, new GUIContent("Additional Materials", "This lets you specify additional materials to have the Kanna Protecc code injected into when you click 'EncryptAvatar'. This will let you encrypt materials used in material swaps."));
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

            if (GUILayout.Button(new GUIContent(HeaderTexture, "Vist my Discord for help!"), EditorStyles.label, GUILayout.Height(Screen.width / 8)))
            {
                Application.OpenURL("https://discord.gg/SyZcuTPXZA");
            }
            
            var KannaProteccRoot = target as KannaProteccRoot;
            
            //Do the big important buttons
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = !KannaProteccRoot.IsProtected;

            if (GUILayout.Button(new GUIContent("Encrypt Avatar", "Validate the AnimatorController, then create encrypted avatar."), GUILayout.Height(Screen.width / 10), GUILayout.Width((Screen.width / 2) - 20f)))
            {
                KannaProteccRoot.EncryptAvatar();
            }

            GUI.enabled = SceneManager.GetActiveScene().GetRootGameObjects().Any(o => o.name.Contains("Encrypted"));

            if (GUILayout.Button(new GUIContent("Write Keys", "Write your keys to saved attributes!"), GUILayout.Height(Screen.width / 10), GUILayout.Width((Screen.width / 2) - 20f)))
            {
                KannaProteccRoot.WriteBitKeysToExpressions(GameObject.Find(KannaProteccRoot.gameObject.name + "_Encrypted_Obfuscated").GetComponent<VRCAvatarDescriptor>().expressionParameters, true, true);
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            //Do the properties
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width((Screen.width / 2) - 20f));
            m_DistortRatioProperty.floatValue = GUILayout.HorizontalSlider(m_DistortRatioProperty.floatValue, .2f, 1.6f);
            GUILayout.Space(15);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Distort Ratio:");
            GUILayout.FlexibleSpace();
            m_DistortRatioProperty.floatValue = EditorGUILayout.FloatField(m_DistortRatioProperty.floatValue);
            GUILayout.EndHorizontal();
            GUILayout.Label("Set high enough so your encrypted mesh is visually wrecked. Default = .4", EditorStyles.wordWrappedLabel);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width((Screen.width / 2) - 20f));
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
                EditorGUILayout.Space();
                m_IgnoreList.DoLayoutList();
                EditorGUILayout.Space();
                EditorGUILayout.Separator();
            }
            GUILayout.Space(5f);

            //buttons for mats and key lock
            GUILayout.BeginHorizontal();

            if (KannaProteccRoot.IsProtected)
            {
                GUI.backgroundColor = Color.green;
            }

            if (GUILayout.Button(new GUIContent("Unlock materials", "Unlock All Materials In Hierarchy")))
            {
                MenuUtilites.UnlockAllMaterialsInHierarchy(null);
            }

            if (KannaProteccRoot.IsProtected)
            {
                GUI.backgroundColor = origColor;
            }

            if (_lockKeys)
            {
                if (GUILayout.Button(new GUIContent("Unlock bitKeys", "Prevent changes to key selection"), GUILayout.Width((Screen.width / 2) - 20f))) _lockKeys = !_lockKeys;
            }
            else if (GUILayout.Button(new GUIContent("Lock BitKeys", "Prevent changes to key selection"), GUILayout.Width((Screen.width / 2) - 20f))) _lockKeys = !_lockKeys;
            GUILayout.EndHorizontal();

            //draw keys here
            using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginDisabledGroup(_lockKeys);

                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Bitkeys", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("These are the keys used to encrypt the mesh.");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical();

                //Display in 4 columns
                for (var i = 0; i < m_KeysProperty.arraySize/4; i++)
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
                for (var i = m_KeysProperty.arraySize / 2; i < (m_KeysProperty.arraySize / 4) * 3 ; i++)
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

                //Generate key button
                EditorGUILayout.Space();
                if (GUILayout.Button(new GUIContent("Generate new Keys", "Generate new key overriding old one. Will need to write keys again!")))
                {
                    KannaProteccRoot.GenerateNewKey();
                }
                EditorGUI.EndDisabledGroup();
            }
            
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.Separator();
            _debugFoldout = EditorGUILayout.Foldout(_debugFoldout, "Debug");
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
                GUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("bitKeys Length:");

                var keycount = EditorGUILayout.IntField(((KannaProteccRoot)target)._bitKeys.Length);

                if (keycount != ((KannaProteccRoot)target)._bitKeys.Length) // Changed
                {
                    ((KannaProteccRoot)target)._bitKeys = new bool[keycount];
                }

                GUILayout.EndHorizontal();
            }
            //serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Obfuscator Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUILayout.Label("Obfuscated Directory: " + (!string.IsNullOrWhiteSpace(_pathProperty?.stringValue) ? _pathProperty.stringValue : "None"));

            GUI.enabled = !string.IsNullOrEmpty(_pathProperty?.stringValue);

            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(_pathProperty?.stringValue))
            {
                GUI.backgroundColor = Color.green;
            }

            if (GUILayout.Button("Clean Up Obfuscated Files"))
            {
                ((KannaProteccRoot)target).obfuscator.ClearObfuscatedFiles((KannaProteccRoot)target);
            }

            if (!string.IsNullOrEmpty(_pathProperty?.stringValue))
            {
                GUI.backgroundColor = origColor;
            }

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

                EditorGUILayout.Space();

                _disableObjectNameObfuscationProperty.boolValue = !disableObjectNameToggle;
            }

            FeatureToggleFoldout(true, "Parameter Name Obfuscation");

            _excludeParamNamesPropertyList.DoLayoutList();

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
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