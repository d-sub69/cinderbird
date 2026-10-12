using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Cinderbird.EditorTools
{
    /// <summary>
    /// Shared helpers for the per-step setup menu commands. Everything goes through Unity's API
    /// (never hand-edited YAML), records Undo where Unity supports it, and is safe to run twice.
    /// </summary>
    internal static class SetupUtility
    {
        public const string ScenePath = "Assets/_Project/Scenes/Prototype_Feel.unity";
        public const string InputActionsPath = "Assets/_Project/Settings/CinderbirdControls.inputactions";

        /// <summary>Makes the prototype scene the open scene, asking to save other unsaved scenes first.</summary>
        public static bool TryOpenPrototypeScene(out Scene scene)
        {
            scene = default;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play mode before running a setup command.");
                return false;
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return false;
                }

                active = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            scene = active;
            return scene.IsValid();
        }

        /// <summary>
        /// Adds a Button action with one keyboard binding to the controls asset if it isn't there yet,
        /// and returns the reference asset the Inspector would show as "Map/Action".
        /// </summary>
        public static InputActionReference EnsureButtonAction(string mapName, string actionName, string bindingPath)
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
            {
                Debug.LogError($"Input actions not found at {InputActionsPath}.");
                return null;
            }

            InputActionMap map = asset.FindActionMap(mapName, true);
            if (map.FindAction(actionName) == null)
            {
                map.AddAction(actionName, InputActionType.Button, bindingPath, groups: KeyboardBindingGroup(asset));
                // The .inputactions file is JSON owned by the Input System; writing its own serialization keeps ids intact.
                File.WriteAllText(InputActionsPath, asset.ToJson());
                AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceUpdate);
            }

            return FindActionReference(mapName, actionName);
        }

        public static InputActionReference FindActionReference(string mapName, string actionName)
        {
            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(InputActionsPath))
            {
                if (subAsset is InputActionReference reference &&
                    reference.action != null &&
                    reference.action.name == actionName &&
                    reference.action.actionMap != null &&
                    reference.action.actionMap.name == mapName)
                {
                    return reference;
                }
            }

            Debug.LogError($"No action reference for {mapName}/{actionName} in {InputActionsPath}.");
            return null;
        }

        /// <summary>Sets a serialized field the same way dragging into the Inspector would (with Undo).</summary>
        public static bool SetReference(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name} has no serialized field '{fieldName}'.", target);
                return false;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            return true;
        }

        /// <summary>Finds the single project asset of a type (e.g. a settings asset under Data/).</summary>
        public static T FindAsset<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length == 0)
            {
                Debug.LogError($"Setup could not find a {typeof(T).Name} asset in the project.");
                return null;
            }

            if (guids.Length > 1)
            {
                Debug.LogWarning($"Found {guids.Length} {typeof(T).Name} assets; using the first.");
            }

            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>Reads an object reference the way the Inspector shows it, e.g. to reuse a prefab another field points at.</summary>
        public static Object GetReference(Object target, string fieldName)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(fieldName);
            return property != null ? property.objectReferenceValue : null;
        }

        public static bool SetColor(Object target, string fieldName, Color value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name} has no serialized field '{fieldName}'.", target);
                return false;
            }

            property.colorValue = value;
            serialized.ApplyModifiedProperties();
            return true;
        }

        /// <summary>Returns the layer's index, adding it to the first free user layer slot if needed; -1 if none is free.</summary>
        public static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
            {
                return existing;
            }

            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            // Slots 0-7 are Unity's built-in layers.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }

            Debug.LogError($"No free layer slot for '{layerName}'.");
            return -1;
        }

        /// <summary>Sets the layer on an object and its children, leaving out branches that contain a particle system.</summary>
        public static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root.GetComponent<ParticleSystem>() != null)
            {
                return;
            }

            if (root.layer != layer)
            {
                Undo.RecordObject(root, "Set layer");
                root.layer = layer;
            }

            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>Editor-time lookup; the runtime rule against Find calls doesn't apply to setup tooling.</summary>
        public static T FindInScene<T>(string description) where T : Object
        {
            T found = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (found == null)
            {
                Debug.LogError($"Setup could not find {description} ({typeof(T).Name}) in the open scene.");
            }

            return found;
        }

        public static void SaveScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // Matches the bindings that came with the template so the Input Actions editor shows the new ones
        // under the same control scheme.
        private static string KeyboardBindingGroup(InputActionAsset asset)
        {
            foreach (InputControlScheme scheme in asset.controlSchemes)
            {
                if (scheme.name.Contains("Keyboard"))
                {
                    return scheme.bindingGroup;
                }
            }

            return null;
        }
    }
}
