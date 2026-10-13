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
        public const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        public const string InputActionsPath = "Assets/_Project/Settings/CinderbirdControls.inputactions";

        /// <summary>Makes the game scene (Main) the open scene, asking to save other unsaved scenes first.</summary>
        public static bool TryOpenMainScene(out Scene scene)
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
                SaveInputActions(asset);
            }

            return FindActionReference(mapName, actionName);
        }

        /// <summary>Adds a Value/Axis action with one binding (e.g. the mouse wheel) if it isn't there yet.</summary>
        public static InputActionReference EnsureAxisAction(string mapName, string actionName, string bindingPath)
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
                map.AddAction(actionName, InputActionType.Value, bindingPath, expectedControlLayout: "Axis");
                SaveInputActions(asset);
            }

            return FindActionReference(mapName, actionName);
        }

        /// <summary>Renames an action, keeping its id and bindings, unless the new name is already taken.</summary>
        public static void RenameAction(string mapName, string oldName, string newName)
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            InputActionMap map = asset != null ? asset.FindActionMap(mapName, true) : null;
            InputAction action = map?.FindAction(oldName);
            if (action == null || map.FindAction(newName) != null)
            {
                return;
            }

            action.Rename(newName);
            SaveInputActions(asset);
        }

        private static void SaveInputActions(InputActionAsset asset)
        {
            // The .inputactions file is JSON owned by the Input System; writing its own serialization keeps ids intact.
            File.WriteAllText(InputActionsPath, asset.ToJson());
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceUpdate);
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
            MarkModified(target);
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

        /// <summary>Loads a ScriptableObject asset, creating it with default values if it doesn't exist yet.</summary>
        public static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Sets a serialized field of any simple type (string, float, bool) through the Inspector path.</summary>
        public static bool SetValue(Object target, string fieldName, System.Action<SerializedProperty> assign)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name} has no serialized field '{fieldName}'.", target);
                return false;
            }

            assign(property);
            serialized.ApplyModifiedProperties();
            MarkModified(target);
            return true;
        }

        // Assets (unlike scene objects) are only written when flagged dirty; flag them explicitly so
        // AssetDatabase.SaveAssets always writes setup changes to disk.
        private static void MarkModified(Object target)
        {
            if (EditorUtility.IsPersistent(target))
            {
                EditorUtility.SetDirty(target);
            }
        }

        /// <summary>Fills a serialized array of object references (e.g. a list of actions or images).</summary>
        public static bool SetReferenceArray(Object target, string fieldName, Object[] values)
        {
            return SetValue(target, fieldName, property =>
            {
                property.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }
            });
        }

        public static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent(out T existing) ? existing : Undo.AddComponent<T>(target);
        }

        /// <summary>Moves a transform under a new parent without changing where it is in the world (with Undo).</summary>
        public static void Reparent(Transform child, Transform newParent)
        {
            if (child.parent != newParent)
            {
                Undo.SetTransformParent(child, newParent, "Reparent");
            }
        }

        /// <summary>Moves a pivot while its children stay put in the world, so only the rotation point changes.</summary>
        public static void MovePivotKeepingChildren(Transform pivot, Vector3 newLocalPosition)
        {
            int count = pivot.childCount;
            Vector3[] positions = new Vector3[count];
            Quaternion[] rotations = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                Transform child = pivot.GetChild(i);
                Undo.RecordObject(child, "Move pivot");
                positions[i] = child.position;
                rotations[i] = child.rotation;
            }

            Undo.RecordObject(pivot, "Move pivot");
            pivot.localPosition = newLocalPosition;
            for (int i = 0; i < count; i++)
            {
                Transform child = pivot.GetChild(i);
                child.position = positions[i];
                child.rotation = rotations[i];
            }
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
            MarkModified(target);
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
