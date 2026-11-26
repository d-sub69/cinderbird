using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>
    /// A copy of the mountain's changed chunks (ash densities, layers, buried embers), kept between saves:
    /// <see cref="Update"/> re-copies only chunks that changed since it last ran, into the same buffers, so an
    /// autosave stays cheap however much of the mountain has been dug. <see cref="Encode"/> only reads this copy,
    /// so it can run on a background thread while the game keeps changing the terrain, as long as the next
    /// <see cref="Update"/> waits for it to finish.
    /// Densities are stored as one signed byte each (-127..127 for -1..1): at most 1/254 off, far below anything
    /// visible, and a quarter of the size before compression.
    /// </summary>
    public sealed class TerrainSnapshot
    {
        // Bump when the binary layout below changes; older data is then rejected rather than misread.
        // 2: each buried ember also stores its cluster (format 1 still loads, its embers in no cluster).
        private const int FormatVersion = 2;
        private const float QuantizeScale = 127f;

        // Per chunk index (null / false where the chunk isn't in the snapshot).
        // Densities are quantized and stored as raw bytes (two's complement sbytes) so they can be written as-is.
        private readonly byte[][] _densities;
        private readonly byte[][] _layers;
        private readonly List<Ember>[] _embers;
        private readonly int[] _copiedVersions;
        // For WarmUp: each chunk's version when last looked at, and for how many calls it has stayed the same.
        private readonly int[] _seenVersions;
        private readonly int[] _unchangedCalls;
        private readonly bool[] _included;
        private readonly List<int> _chunkIndices = new List<int>();

        public TerrainSnapshot(int samplesPerChunk, int worldChunkCount)
        {
            if (samplesPerChunk <= 0 || worldChunkCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(samplesPerChunk), "A snapshot needs a size.");
            }

            SamplesPerChunk = samplesPerChunk;
            _densities = new byte[worldChunkCount][];
            _layers = new byte[worldChunkCount][];
            _embers = new List<Ember>[worldChunkCount];
            _copiedVersions = new int[worldChunkCount];
            _seenVersions = new int[worldChunkCount];
            _unchangedCalls = new int[worldChunkCount];
            _included = new bool[worldChunkCount];
        }

        /// <summary>Samples in one chunk; a snapshot only fits worlds with the same chunk size.</summary>
        public int SamplesPerChunk { get; }

        public int ChunkCount => _chunkIndices.Count;

        /// <summary>The chunks in the snapshot, in increasing order.</summary>
        public IReadOnlyList<int> ChunkIndices => _chunkIndices;

        /// <summary>How many chunks the last <see cref="Update"/> actually copied; the rest were unchanged.</summary>
        public int LastCopiedChunkCount { get; private set; }

        /// <summary>A fresh snapshot of every chunk changed since the mountain was generated.</summary>
        public static TerrainSnapshot Capture(AshTerrain terrain)
        {
            VoxelWorld world = terrain.World;
            TerrainSnapshot snapshot = new TerrainSnapshot(world.GetChunk(0).Densities.Length, world.ChunkCount);
            snapshot.Update(terrain);
            return snapshot;
        }

        /// <summary>
        /// Brings the snapshot up to date with the terrain, copying only chunks that changed since the last call.
        /// Must not run while <see cref="Encode"/> is running on another thread.
        /// </summary>
        public void Update(AshTerrain terrain)
        {
            VoxelWorld world = terrain.World;
            if (world.ChunkCount != _included.Length || world.GetChunk(0).Densities.Length != SamplesPerChunk)
            {
                throw new InvalidOperationException("This snapshot was made for a world of another size.");
            }

            LastCopiedChunkCount = 0;
            _chunkIndices.Clear();
            for (int i = 0; i < _included.Length; i++)
            {
                if (!terrain.IsChunkModified(i))
                {
                    _included[i] = false;
                    continue;
                }

                int version = terrain.ChangeVersionOf(i);
                if (!_included[i] || _copiedVersions[i] != version)
                {
                    CopyChunk(terrain, i);
                    _copiedVersions[i] = version;
                    _included[i] = true;
                    LastCopiedChunkCount++;
                }

                _chunkIndices.Add(i);
            }
        }

        /// <summary>
        /// Copies ahead of a save, a few at a time, changed chunks that have stopped changing (the same for
        /// <paramref name="unchangedCalls"/> calls in a row), so the save's own <see cref="Update"/> has little left to
        /// copy. Chunks still changing (sliding ash) are left for later. Same rule as Update: never while
        /// <see cref="Encode"/> runs.
        /// </summary>
        /// <returns>How many chunks were copied.</returns>
        public int WarmUp(AshTerrain terrain, int maxChunks, int unchangedCalls)
        {
            int copied = 0;
            for (int i = 0; i < _included.Length; i++)
            {
                if (!terrain.IsChunkModified(i))
                {
                    continue;
                }

                int version = terrain.ChangeVersionOf(i);
                if (version != _seenVersions[i])
                {
                    _seenVersions[i] = version;
                    _unchangedCalls[i] = 0;
                    continue;
                }

                _unchangedCalls[i]++;
                if (copied >= maxChunks || _unchangedCalls[i] < unchangedCalls || (_included[i] && _copiedVersions[i] == version))
                {
                    continue;
                }

                CopyChunk(terrain, i);
                _copiedVersions[i] = version;
                _included[i] = true;
                copied++;
            }

            return copied;
        }

        private void CopyChunk(AshTerrain terrain, int index)
        {
            VoxelGrid chunk = terrain.World.GetChunk(index);
            float[] densities = chunk.Densities;
            byte[] packed = _densities[index] ??= new byte[SamplesPerChunk];
            for (int s = 0; s < densities.Length; s++)
            {
                packed[s] = (byte)Quantize(densities[s]);
            }

            byte[] layers = _layers[index] ??= new byte[SamplesPerChunk];
            Array.Copy(chunk.Layers, layers, SamplesPerChunk);

            List<Ember> embers = _embers[index] ??= new List<Ember>();
            embers.Clear();
            EmberField field = terrain.EmbersIn(index);
            for (int e = 0; e < field.Count; e++)
            {
                embers.Add(field[e]);
            }
        }

        public static sbyte Quantize(float density)
        {
            return (sbyte)Mathf.RoundToInt(Mathf.Clamp(density, VoxelGrid.Air, VoxelGrid.Ash) * QuantizeScale);
        }

        public static float Dequantize(sbyte value)
        {
            return value / QuantizeScale;
        }

        /// <summary>Packs the snapshot into gzip-compressed bytes as base64 text. Safe on a background thread.</summary>
        public string Encode()
        {
            using MemoryStream output = new MemoryStream();
            using (GZipStream zip = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, true))
            using (BinaryWriter writer = new BinaryWriter(zip))
            {
                writer.Write(FormatVersion);
                writer.Write(SamplesPerChunk);
                writer.Write(_chunkIndices.Count);
                foreach (int index in _chunkIndices)
                {
                    writer.Write(index);
                    writer.Write(_densities[index]);
                    writer.Write(_layers[index]);

                    List<Ember> embers = _embers[index];
                    writer.Write(embers.Count);
                    foreach (Ember ember in embers)
                    {
                        writer.Write(ember.Position.x);
                        writer.Write(ember.Position.y);
                        writer.Write(ember.Position.z);
                        writer.Write((ushort)ember.TierIndex);
                        writer.Write(ember.Layer);
                        writer.Write(ember.Cluster);
                    }
                }
            }

            return Convert.ToBase64String(output.ToArray());
        }

        /// <exception cref="InvalidDataException">The text isn't a terrain snapshot this version can read.</exception>
        public static TerrainSnapshot Decode(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                throw new InvalidDataException("No terrain data.");
            }

            try
            {
                using MemoryStream input = new MemoryStream(Convert.FromBase64String(data));
                using GZipStream zip = new GZipStream(input, CompressionMode.Decompress);
                using BinaryReader reader = new BinaryReader(zip);
                return Read(reader);
            }
            catch (Exception e) when (e is FormatException || e is IOException)
            {
                throw new InvalidDataException("Terrain data is damaged: " + e.Message, e);
            }
        }

        private static TerrainSnapshot Read(BinaryReader reader)
        {
            int format = reader.ReadInt32();
            if (format != FormatVersion && format != 1)
            {
                throw new InvalidDataException($"Unknown terrain data format {format}.");
            }

            int samplesPerChunk = reader.ReadInt32();
            int chunkCount = reader.ReadInt32();
            if (samplesPerChunk <= 0 || chunkCount < 0)
            {
                throw new InvalidDataException("Terrain data has an impossible size.");
            }

            // Read everything first: the snapshot is sized by the highest chunk index it holds.
            int[] indices = new int[chunkCount];
            byte[][] densities = new byte[chunkCount][];
            byte[][] layers = new byte[chunkCount][];
            List<Ember>[] embers = new List<Ember>[chunkCount];
            int highest = -1;
            for (int c = 0; c < chunkCount; c++)
            {
                indices[c] = reader.ReadInt32();
                if (indices[c] < 0 || (c > 0 && indices[c] <= indices[c - 1]))
                {
                    throw new InvalidDataException("Terrain data has chunks out of order.");
                }

                highest = indices[c];
                densities[c] = ReadExactly(reader, samplesPerChunk);
                layers[c] = ReadExactly(reader, samplesPerChunk);

                int emberCount = reader.ReadInt32();
                if (emberCount < 0)
                {
                    throw new InvalidDataException("Terrain data has a negative ember count.");
                }

                List<Ember> chunkEmbers = new List<Ember>(emberCount);
                for (int e = 0; e < emberCount; e++)
                {
                    Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    int tier = reader.ReadUInt16();
                    byte layer = reader.ReadByte();
                    ushort cluster = format >= 2 ? reader.ReadUInt16() : (ushort)0;
                    chunkEmbers.Add(new Ember(position, tier, layer, cluster));
                }

                embers[c] = chunkEmbers;
            }

            TerrainSnapshot snapshot = new TerrainSnapshot(samplesPerChunk, Math.Max(1, highest + 1));
            for (int c = 0; c < chunkCount; c++)
            {
                int index = indices[c];
                snapshot._densities[index] = densities[c];
                snapshot._layers[index] = layers[c];
                snapshot._embers[index] = embers[c];
                snapshot._included[index] = true;
                snapshot._chunkIndices.Add(index);
            }

            return snapshot;
        }

        private static byte[] ReadExactly(BinaryReader reader, int count)
        {
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new EndOfStreamException("Terrain data ends early.");
            }

            return bytes;
        }

        /// <summary>Whether every chunk in the snapshot exists in this world and has the same size.</summary>
        public bool FitsWorld(VoxelWorld world)
        {
            if (world.GetChunk(0).Densities.Length != SamplesPerChunk)
            {
                return false;
            }

            foreach (int index in _chunkIndices)
            {
                if (index >= world.ChunkCount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Overwrites the saved chunks of a freshly generated mountain and marks them for re-meshing.
        /// Shared border samples are written through the world so neighboring chunks keep matching copies.
        /// </summary>
        /// <exception cref="InvalidDataException">When the snapshot doesn't fit the world; nothing is changed then.</exception>
        public void ApplyTo(AshTerrain terrain)
        {
            VoxelWorld world = terrain.World;
            if (!FitsWorld(world))
            {
                throw new InvalidDataException("Terrain data doesn't match this world's size.");
            }

            foreach (int index in _chunkIndices)
            {
                ApplyChunk(world, index, _densities[index], _layers[index]);
                terrain.ReplaceEmbers(index, _embers[index]);
            }

            terrain.FinishRestore(_chunkIndices);
        }

        private static void ApplyChunk(VoxelWorld world, int index, byte[] densities, byte[] layers)
        {
            VoxelGrid chunk = world.GetChunk(index);
            float[] target = chunk.Densities;
            for (int s = 0; s < target.Length; s++)
            {
                target[s] = Dequantize((sbyte)densities[s]);
            }

            Array.Copy(layers, chunk.Layers, layers.Length);

            // The interior belongs to this chunk alone; only the faces are shared with neighbors.
            Vector3Int offset = world.ChunkSampleOffset(index);
            for (int z = 0; z < chunk.SamplesZ; z++)
            {
                bool faceZ = z == 0 || z == chunk.SamplesZ - 1;
                for (int y = 0; y < chunk.SamplesY; y++)
                {
                    bool faceY = faceZ || y == 0 || y == chunk.SamplesY - 1;
                    for (int x = 0; x < chunk.SamplesX; x++)
                    {
                        if (!faceY && x != 0 && x != chunk.SamplesX - 1)
                        {
                            continue;
                        }

                        int sample = chunk.Index(x, y, z);
                        world.SetDensity(offset.x + x, offset.y + y, offset.z + z, target[sample]);
                        world.SetLayer(offset.x + x, offset.y + y, offset.z + z, chunk.Layers[sample]);
                    }
                }
            }

            world.MarkDirty(index);
        }
    }
}
