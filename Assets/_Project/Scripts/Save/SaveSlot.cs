using System.IO;

namespace Cinderbird.Save
{
    /// <summary>
    /// One save slot: writes a <see cref="SaveFile"/> safely, and loads it with fallbacks. A damaged save falls
    /// back to the backup; if both are damaged they're moved aside and the game starts fresh. No Unity scene
    /// access, so <see cref="Write"/> can run on a background thread.
    /// </summary>
    public sealed class SaveSlot
    {
        private readonly SaveFileStore _store;

        public SaveSlot(SaveFileStore store)
        {
            _store = store;
        }

        public SaveFileStore Store => _store;

        /// <summary>Packs the terrain into the file and writes it.</summary>
        /// <param name="terrain">Changed chunks to store; null leaves the file's terrain data as it is.</param>
        public void Write(SaveFile file, TerrainSnapshot terrain)
        {
            if (terrain != null)
            {
                file.Terrain.Chunks = terrain.Encode();
                file.Terrain.ChunkCount = terrain.ChunkCount;
            }

            _store.Write(SaveSerializer.ToJson(file));
        }

        /// <summary>Reads and fully checks the save, including the packed terrain.</summary>
        /// <param name="warning">Something the player should hear about (a fallback was used, saves were damaged); null otherwise.</param>
        /// <returns>False when there's no usable save; start a new game then.</returns>
        public bool TryLoad(out SaveFile file, out TerrainSnapshot terrain, out string warning)
        {
            warning = null;
            if (!_store.HasAnySave)
            {
                file = null;
                terrain = null;
                return false;
            }

            if (TryRead(_store.ReadMain(), out file, out terrain, out string mainError))
            {
                return true;
            }

            if (TryRead(_store.ReadBackup(), out file, out terrain, out _))
            {
                warning = $"The save couldn't be read ({mainError}); loaded the previous one instead.";
                return true;
            }

            _store.Quarantine();
            warning = $"The save couldn't be read ({mainError}); starting a new game. The damaged files were kept next to the save.";
            return false;
        }

        public void Delete()
        {
            _store.Delete();
        }

        private static bool TryRead(string json, out SaveFile file, out TerrainSnapshot terrain, out string error)
        {
            terrain = null;
            if (json == null)
            {
                file = null;
                error = "the file is missing";
                return false;
            }

            if (!SaveSerializer.TryParse(json, out file, out error))
            {
                return false;
            }

            if (string.IsNullOrEmpty(file.Terrain.Chunks))
            {
                // Nothing dug yet: the save carries no chunks.
                return true;
            }

            try
            {
                terrain = TerrainSnapshot.Decode(file.Terrain.Chunks);
                return true;
            }
            catch (InvalidDataException e)
            {
                file = null;
                error = e.Message;
                return false;
            }
        }
    }
}
