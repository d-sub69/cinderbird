using System;
using System.Collections.Generic;
using System.Globalization;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Player;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>
    /// Copies the game state into a <see cref="SaveFile"/> and back: the mountain, currencies, player, tools,
    /// sieve and basket. Knows nothing about files or timing; <see cref="SaveController"/> decides when.
    /// </summary>
    public sealed class SaveStateMapper
    {
        private readonly VoxelTerrain _pile;
        private readonly Wallet _wallet;
        private readonly FirstPersonController _player;
        private readonly ToolBelt _toolBelt;
        private readonly Sieve _sieve;
        private readonly SieveFiller _sieveFiller;
        private readonly Basket _basket;
        private readonly PlaceableTool[] _placeableTools;
        private readonly List<ToolDefinition> _slotBuffer = new List<ToolDefinition>();
        private readonly List<ToolDefinition> _placedBuffer = new List<ToolDefinition>();
        // Kept between saves, so only chunks changed since the last one are copied.
        private TerrainSnapshot _terrainSnapshot;

        public SaveStateMapper(VoxelTerrain pile, Wallet wallet, FirstPersonController player, ToolBelt toolBelt,
            Sieve sieve, SieveFiller sieveFiller, Basket basket, PlaceableTool[] placeableTools)
        {
            _pile = pile;
            _wallet = wallet;
            _player = player;
            _toolBelt = toolBelt;
            _sieve = sieve;
            _sieveFiller = sieveFiller;
            _basket = basket;
            _placeableTools = placeableTools;
        }

        /// <summary>
        /// Copies the current state. Open any hole still draining first (<see cref="VoxelTerrain.CompleteScoopNow"/>):
        /// its ash is already in the sieve. The terrain snapshot is reused between captures, so only call this while no
        /// background write (which reads it) is running.
        /// </summary>
        /// <param name="terrain">The mountain's changed chunks; null before the mountain is built.</param>
        public SaveFile Capture(out TerrainSnapshot terrain)
        {
            SaveFile file = new SaveFile
            {
                SavedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            };

            AshTerrain ash = _pile.Terrain;
            terrain = null;
            if (ash != null)
            {
                _terrainSnapshot ??= new TerrainSnapshot(ash.World.GetChunk(0).Densities.Length, ash.World.ChunkCount);
                _terrainSnapshot.Update(ash);
                terrain = _terrainSnapshot;
                IReadOnlyList<LooseEmber> loose = ash.LooseEmbers;
                for (int i = 0; i < loose.Count; i++)
                {
                    file.Terrain.LooseEmbers.Add(new EmberRecord(loose[i].Ember));
                }
            }

            file.Terrain.Fingerprint = _pile.Fingerprint ?? string.Empty;
            file.Terrain.DeepestUnlockedLayer = _pile.DeepestUnlockedLayer;
            file.Currencies.Glow = _wallet.Currencies.Glow;
            file.Currencies.Heat = _wallet.Currencies.Heat;
            file.Player.Pose = _player.Pose;
            CaptureTools(file.Tools);
            CaptureSieve(file.Sieve);
            EmberRecord.Fill(file.Basket.Embers, _basket.Contents.Embers);
            return file;
        }

        /// <summary>
        /// Puts a loaded save on top of the freshly built mountain. If the mountain's settings changed since the save
        /// (another fingerprint), the mountain stays fresh: Glow, Heat, open layers and tools are kept, placed tools
        /// come back to the hands and the player starts at the default spot.
        /// </summary>
        /// <param name="lostBasketEmbers">Saved basket embers that didn't fit any more.</param>
        /// <returns>Whether the saved mountain was restored (same fingerprint).</returns>
        public bool Apply(SaveFile file, TerrainSnapshot terrain, out int lostBasketEmbers)
        {
            AshTerrain ash = _pile.Terrain;
            bool sameMountain = ash != null && file.Terrain.Fingerprint == _pile.Fingerprint &&
                                (terrain == null || terrain.FitsWorld(ash.World));
            if (ash != null)
            {
                ash.Locks.UnlockThrough(file.Terrain.DeepestUnlockedLayer);
                if (sameMountain)
                {
                    // Before the chunks: restoring them recounts what is still on the mountain, loose embers included.
                    ash.ReplaceLoose(EmberRecord.ToEmbers(file.Terrain.LooseEmbers));
                }

                if (sameMountain && terrain != null)
                {
                    terrain.ApplyTo(ash);
                }

                _pile.CommitRestore();
            }

            _wallet.Currencies.Restore(Math.Max(0, file.Currencies.Glow), Math.Max(0, file.Currencies.Heat));
            SieveSave sieve = file.Sieve;
            _sieve.Contents.Restore(Mathf.Max(0f, sieve.AshVolume), sieve.LoadVolume, sieve.AshColor, sieve.DustColor,
                EmberRecord.ToEmbers(sieve.Embers));
            lostBasketEmbers = _basket.Contents.Restore(EmberRecord.ToEmbers(file.Basket.Embers));
            RestoreTools(file.Tools, sameMountain);
            if (sameMountain)
            {
                _player.RestorePose(file.Player.Pose);
            }

            return sameMountain;
        }

        private void RestoreTools(ToolsSave saved, bool restorePlaced)
        {
            _placedBuffer.Clear();
            if (restorePlaced)
            {
                foreach (PlacedToolSave placed in saved.Placed)
                {
                    PlaceableTool tool = FindPlaceable(placed.ToolId);
                    if (tool != null && !_placedBuffer.Contains(tool.Tool))
                    {
                        _placedBuffer.Add(tool.Tool);
                    }
                }
            }

            _slotBuffer.Clear();
            foreach (string id in saved.Slots)
            {
                ToolDefinition tool = _toolBelt.FindTool(id);
                // A tool can't be both in a slot and out in the world.
                _slotBuffer.Add(tool != null && !_placedBuffer.Contains(tool) ? tool : null);
            }

            _toolBelt.RestoreSlots(_slotBuffer, saved.ActiveSlot, _placedBuffer);

            if (!restorePlaced)
            {
                return;
            }

            foreach (PlacedToolSave placed in saved.Placed)
            {
                PlaceableTool tool = FindPlaceable(placed.ToolId);
                if (tool != null && !tool.IsPlaced)
                {
                    tool.PlaceAt(placed.Position - Vector3.up * tool.PlacedHeightOffset, placed.Rotation);
                }
            }
        }

        private PlaceableTool FindPlaceable(string toolId)
        {
            foreach (PlaceableTool tool in _placeableTools)
            {
                if (tool != null && ToolBelt.IdOf(tool.Tool) == toolId)
                {
                    return tool;
                }
            }

            return null;
        }

        private void CaptureTools(ToolsSave save)
        {
            Inventory<ToolDefinition> inventory = _toolBelt.Inventory;
            for (int i = 0; i < inventory.Capacity; i++)
            {
                save.Slots.Add(ToolBelt.IdOf(inventory[i]));
            }

            save.ActiveSlot = inventory.ActiveSlot;
            foreach (PlaceableTool tool in _placeableTools)
            {
                if (tool != null && tool.IsPlaced)
                {
                    Transform pose = tool.transform;
                    save.Placed.Add(new PlacedToolSave(ToolBelt.IdOf(tool.Tool), pose.position, pose.rotation));
                }
            }
        }

        private void CaptureSieve(SieveSave save)
        {
            SieveContents contents = _sieve.Contents;
            EmberRecord.Fill(save.Embers, contents.Embers);
            if (!_sieveFiller.IsFilling)
            {
                save.AshVolume = contents.AshVolume;
                save.LoadVolume = contents.LoadVolume;
                save.AshColor = contents.AshColor;
                save.DustColor = contents.DustColor;
                return;
            }

            // A load still pouring in already left the mountain, so it counts as in the sieve. Pours only start
            // into an ash-free sieve, so it is the whole load.
            IReadOnlyList<Ember> incoming = _sieveFiller.IncomingEmbers;
            for (int i = 0; i < incoming.Count; i++)
            {
                save.Embers.Add(new EmberRecord(incoming[i]));
            }

            save.AshVolume = contents.AshVolume + _sieveFiller.IncomingVolume;
            save.LoadVolume = save.AshVolume;
            save.AshColor = _sieveFiller.IncomingAshColor;
            save.DustColor = _sieveFiller.IncomingDustColor;
        }
    }
}
