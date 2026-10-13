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
    /// A copy of the mountain's changed chunks (ash densities, layers, buried embers). Taken on the main thread
    /// in one quick pass; <see cref="Encode"/> only touches this copy, so it can run on a background thread
    /// while the game keeps changing the terrain.
    /// Densities are stored as one signed byte each (-127..127 for -1..1): at most 1/254 off, far below anything
    /// visible, and a quarter of the size before compression.
    /// </summary>
    public sealed class TerrainSnapshot
    {
        // Bump when the binary layout below changes; older data is then rejected rather than misread.
        private const int FormatVersion = 1;
        private const float QuantizeScale = 127f;

        private readonly List<int> _chunkIndices = new List<int>();
        // Quantized densities as raw bytes (two's complement sbytes), so they can be written without conversion.
        private readonly List<byte[]> _densities = new List<byte[]>();
        private readonly List<byte[]> _layers = new List<byte[]>();
        private readonly List<Ember[]> _embers = new List<Ember[]>();

        private TerrainSnapshot(int samplesPerChunk)
        {
            SamplesPerChunk = samplesPerChunk;
        }

        /// <summary>Samples in one chunk; a snapshot only fits worlds with the same chunk size.</summary>
        public int SamplesPerChunk { get; }

        public int ChunkCount => _chunkIndices.Count;

        public IReadOnlyList<int> ChunkIndices => _chunkIndices;

        /// <summary>Copies every chunk whose ash or embers changed since the mountain was generated.</summary>
        public static TerrainSnapshot Capture(AshTerrain terrain)
        {
            VoxelWorld world = terrain.World;
            TerrainSnapshot snapshot = new TerrainSnapshot(world.GetChunk(0).Densities.Length);
            for (int i = 0; i < world.ChunkCount; i++)
            {
                if (!terrain.IsChunkModified(i))
                {
                    continue;
                }

                VoxelGrid chunk = world.GetChunk(i);
                float[] densities = chunk.Densities;
                byte[] packed = new byte[densities.Length];
                for (int s = 0; s < densities.Length; s++)
                {
                    packed[s] = (byte)Quantize(densities[s]);
                }

                EmberField field = terrain.EmbersIn(i);
                Ember[] embers = new Ember[field.Count];
                for (int e = 0; e < embers.Length; e++)
                {
                    embers[e] = field[e];
                }

                snapshot._chunkIndices.Add(i);
                snapshot._densities.Add(packed);
                snapshot._layers.Add((byte[])chunk.Layers.Clone());
                snapshot._embers.Add(embers);
            }

            return snapshot;
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
                for (int c = 0; c < _chunkIndices.Count; c++)
                {
                    writer.Write(_chunkIndices[c]);
                    writer.Write(_densities[c]);
                    writer.Write(_layers[c]);

                    Ember[] embers = _embers[c];
                    writer.Write(embers.Length);
                    foreach (Ember ember in embers)
                    {
                        writer.Write(ember.Position.x);
                        writer.Write(ember.Position.y);
                        writer.Write(ember.Position.z);
                        writer.Write((ushort)ember.TierIndex);
                        writer.Write(ember.Layer);
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
            if (format != FormatVersion)
            {
                throw new InvalidDataException($"Unknown terrain data format {format}.");
            }

            int samplesPerChunk = reader.ReadInt32();
            int chunkCount = reader.ReadInt32();
            if (samplesPerChunk <= 0 || chunkCount < 0)
            {
                throw new InvalidDataException("Terrain data has an impossible size.");
            }

            TerrainSnapshot snapshot = new TerrainSnapshot(samplesPerChunk);
            for (int c = 0; c < chunkCount; c++)
            {
                snapshot._chunkIndices.Add(reader.ReadInt32());
                snapshot._densities.Add(ReadExactly(reader, samplesPerChunk));
                snapshot._layers.Add(ReadExactly(reader, samplesPerChunk));

                int emberCount = reader.ReadInt32();
                if (emberCount < 0)
                {
                    throw new InvalidDataException("Terrain data has a negative ember count.");
                }

                Ember[] embers = new Ember[emberCount];
                for (int e = 0; e < emberCount; e++)
                {
                    Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    int tier = reader.ReadUInt16();
                    byte layer = reader.ReadByte();
                    embers[e] = new Ember(position, tier, layer);
                }

                snapshot._embers.Add(embers);
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
                if (index < 0 || index >= world.ChunkCount)
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

            for (int c = 0; c < _chunkIndices.Count; c++)
            {
                int index = _chunkIndices[c];
                ApplyChunk(world, index, _densities[c], _layers[c]);
                terrain.ReplaceEmbers(index, _embers[c]);
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
