using UnityEngine;

namespace Cinderbird.Save
{
    [CreateAssetMenu(fileName = "SaveSettings", menuName = "Cinderbird/Save Settings")]
    public sealed class SaveSettings : ScriptableObject
    {
        [Tooltip("Folder inside the player's data folder (Application.persistentDataPath).")]
        [SerializeField] private string _folderName = "Saves";
        [SerializeField] private string _fileName = "save.json";

        [Header("Autosave")]
        [Tooltip("Seconds between autosaves; 0 turns timed autosaves off (feeding and quitting still save).")]
        [SerializeField, Min(0f)] private float _autosaveInterval = 60f;
        [SerializeField] private bool _saveAfterFeeding = true;

        [Header("Editor")]
        [Tooltip("Off: every Play in the Editor starts a fresh mountain (autosave still writes the save). Builds always load.")]
        [SerializeField] private bool _loadSaveInEditor = true;

        public string FolderName => _folderName;
        public string FileName => _fileName;
        public float AutosaveInterval => _autosaveInterval;
        public bool SaveAfterFeeding => _saveAfterFeeding;
        public bool LoadSaveInEditor => _loadSaveInEditor;
    }
}
