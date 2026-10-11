using System.Text;
using Cinderbird.Embers;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using TMPro;
using UnityEngine;

namespace Cinderbird.UI
{
    /// <summary>
    /// Prototype read-out of the sieve and pile. Rebuilds its text only when something changes.
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Sieve _sieve;
        [SerializeField] private VoxelChunk _pile;
        [SerializeField] private EmberTierCatalog _tierCatalog;

        private readonly StringBuilder _builder = new StringBuilder();
        private int[] _tierCounts;

        private void Awake()
        {
            if (_text == null || _sieve == null || _pile == null || _tierCatalog == null)
            {
                Debug.LogError($"{nameof(DebugHud)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _tierCounts = new int[_tierCatalog.Count];
        }

        private void OnEnable()
        {
            if (_tierCounts == null)
            {
                return;
            }

            _sieve.Contents.Changed += Refresh;
            _pile.Dug += OnDug;
        }

        // Start rather than OnEnable: the pile builds its grid in Awake, which may run after ours.
        private void Start()
        {
            Refresh();
        }

        private void OnDisable()
        {
            if (_tierCounts == null)
            {
                return;
            }

            _sieve.Contents.Changed -= Refresh;
            _pile.Dug -= OnDug;
        }

        private void OnDug(DigResult result)
        {
            Refresh();
        }

        private void Refresh()
        {
            SieveContents contents = _sieve.Contents;
            System.Array.Clear(_tierCounts, 0, _tierCounts.Length);
            for (int i = 0; i < contents.Embers.Count; i++)
            {
                int tier = contents.Embers[i].TierIndex;
                if (tier >= 0 && tier < _tierCounts.Length)
                {
                    _tierCounts[tier]++;
                }
            }

            _builder.Clear();
            _builder.Append("Sieve: ").Append((contents.AshVolume * 1000f).ToString("F0")).Append(" L ash");
            _builder.Append(contents.CanScoop ? "" : "  (shake before scooping)").AppendLine();
            _builder.Append("Embers in sieve: ").Append(contents.Embers.Count);
            for (int i = 0; i < _tierCounts.Length; i++)
            {
                if (_tierCounts[i] > 0 && _tierCatalog[i] != null)
                {
                    _builder.Append("  ").Append(_tierCatalog[i].DisplayName).Append(' ').Append(_tierCounts[i]);
                }
            }

            _builder.AppendLine();
            _builder.Append("Pile: ").Append(_pile.TotalAshVolume.ToString("F2")).Append(" m³, ")
                .Append(_pile.BuriedEmberCount).Append(" embers buried");

            _text.SetText(_builder);
        }
    }
}
