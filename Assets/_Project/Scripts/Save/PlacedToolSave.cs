using System;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>A tool standing in the world (e.g. the basket) and its exact pose.</summary>
    [Serializable]
    public sealed class PlacedToolSave
    {
        [SerializeField] private string _toolId = string.Empty;
        [SerializeField] private Vector3 _position;
        [SerializeField] private Quaternion _rotation = Quaternion.identity;

        public PlacedToolSave()
        {
        }

        public PlacedToolSave(string toolId, Vector3 position, Quaternion rotation)
        {
            _toolId = toolId;
            _position = position;
            _rotation = rotation;
        }

        public string ToolId => _toolId;
        public Vector3 Position => _position;
        public Quaternion Rotation => _rotation;
    }
}
