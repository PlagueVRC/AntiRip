using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using GeoTetra.GTAvaUtil;

namespace GeoTetra.GTAvaCrypt
{
    [CustomEditor(typeof(AvaCryptV2Root))]
    [CanEditMultipleObjects]
    public class AvaCryptRootEditor : Editor
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

        Texture2D HeaderTexture;
        void OnEnable()
        {
            m_DistortRatioProperty = serializedObject.FindProperty("_distortRatio");
            m_KeysProperty = serializedObject.FindProperty("_bitKeys");
            m_VrcSavedParamsPathProperty = serializedObject.FindProperty("_vrcSavedParamsPath");
            HeaderTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/com.geotetra.gtavacrypt/Textures/Titlebar.png", typeof(Texture2D));
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
        }

        void AdditionalDrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Additional Materials");
        }

        void AdditionalDrawListItems(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = m_AdditionalList.serializedProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element);
        }

        void IgnoreDrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Ignored Materials");
        }

        void IgnoreDrawListItems(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = m_IgnoreList.serializedProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (GUILayout.Button(new GUIContent(HeaderTexture, "Vist my Discord for help!"), EditorStyles.label, GUILayout.Height(Screen.width / 8)))
            {
                Application.OpenURL("https://discord.gg/nbzqtaVP9J");
            }
            
            AvaCryptV2Root avaCryptV2Root = target as AvaCryptV2Root;
            
            //Do the big important buttons
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Encrypt Avatar", "Validate the AnimatorController, then create encrypted avatar."), GUILayout.Height(Screen.width / 10), GUILayout.Width((Screen.width / 2) - 20f)))
            {
                avaCryptV2Root.EncryptAvatar();
            }

            if (GUILayout.Button(new GUIContent("Write Keys", "Write your keys to saved attributes!"), GUILayout.Height(Screen.width / 10), GUILayout.Width((Screen.width / 2) - 20f)))
            {
                avaCryptV2Root.WriteBitKeysToExpressions();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            //Do the properties
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width((Screen.width / 2) - 20f));
            m_DistortRatioProperty.floatValue = GUILayout.HorizontalSlider(m_DistortRatioProperty.floatValue, .1f, .4f);
            GUILayout.Space(15);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Distort Ratio:");
            GUILayout.FlexibleSpace();
            EditorGUILayout.FloatField(m_DistortRatioProperty.floatValue);
            GUILayout.EndHorizontal();
            GUILayout.Label("Set high enough so your encrypted mesh is visuall. Default = .1", EditorStyles.wordWrappedLabel);
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
            GUILayout.Label(new GUIContent("Materials", "By default Avacrypt will inject its code into any Poiyomi 8 materials on this avatar. Here you can adjust that behaviour to include or remove some materials."), EditorStyles.boldLabel);
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(new GUIContent("Additional Materials", "This lets you specify additional materials to have the AvaCrypt code injected into when you click 'EncryptAvatar'. This will let you encrypt materials used in material swaps."));
                m_AdditionalList.DoLayoutList();
                EditorGUILayout.Space();
                GUILayout.Label(new GUIContent("Ignored Materials", "These materials will be ignored by Avacrypt. If a mesh contains other materials that are not ignored it will still be encrypted."));
                m_IgnoreList.DoLayoutList();
                EditorGUILayout.Space();
                EditorGUILayout.Separator();
            }
            GUILayout.Space(5f);

            //buttons for poi mats and key lock
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Unlock Poi mats", "Unlock All Poi Materials In Hierarchy")))
            {
                MenuUtilites.UnlockAllPoiMaterialsInHierarchy(null);
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
                for (int i = 0; i < m_KeysProperty.arraySize/4; i++)
                {
                    GUILayout.BeginHorizontal();
                    m_KeysProperty.GetArrayElementAtIndex(i).boolValue = GUILayout.Toggle(m_KeysProperty.GetArrayElementAtIndex(i).boolValue, "BitKey" + i);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(5f);
                }
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical();
                for (int i = m_KeysProperty.arraySize / 4; i < m_KeysProperty.arraySize / 2; i++)
                {
                    GUILayout.BeginHorizontal();
                    m_KeysProperty.GetArrayElementAtIndex(i).boolValue = GUILayout.Toggle(m_KeysProperty.GetArrayElementAtIndex(i).boolValue, "BitKey" + i);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(5f);
                }
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical();
                for (int i = m_KeysProperty.arraySize / 2; i < (m_KeysProperty.arraySize / 4) * 3 ; i++)
                {
                    GUILayout.BeginHorizontal();
                    m_KeysProperty.GetArrayElementAtIndex(i).boolValue = GUILayout.Toggle(m_KeysProperty.GetArrayElementAtIndex(i).boolValue, "BitKey" + i);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(5f);
                }
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical();
                for (int i = (m_KeysProperty.arraySize / 4) * 3; i < m_KeysProperty.arraySize; i++)
                {
                    GUILayout.BeginHorizontal();
                    m_KeysProperty.GetArrayElementAtIndex(i).boolValue = GUILayout.Toggle(m_KeysProperty.GetArrayElementAtIndex(i).boolValue, "BitKey" + i);
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
                    avaCryptV2Root.GenerateNewKey();
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
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Validate Animator Controller", "Validate all parameters, layers and animations are correct in this avatar's AnimatorController."), GUILayout.Height(Screen.width / 10), GUILayout.Width((Screen.width / 2) - 20f)))
                {
                    avaCryptV2Root.ValidateAnimatorController();
                }

                if (GUILayout.Button(new GUIContent("Delete AvaCrypt Objects From Controller", "Deletes all the objects AvaCrypt wrote to your controller. Try running this if something gets weird with encrypting"), GUILayout.Height(Screen.width / 10), GUILayout.Width((Screen.width / 2) - 20f)))
                {
                    avaCryptV2Root.DeleteAvaCryptObjectsFromController();
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                EditorGUILayout.Space();
                GUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("bitKeys Length:");

                var keycount = EditorGUILayout.IntField(((AvaCryptV2Root)target)._bitKeys.Length);

                if (keycount != ((AvaCryptV2Root)target)._bitKeys.Length) // Changed
                {
                    ((AvaCryptV2Root)target)._bitKeys = new bool[keycount];
                }

                GUILayout.EndHorizontal();
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}