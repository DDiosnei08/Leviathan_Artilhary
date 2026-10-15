using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ProjectLeviathan.Grass
{
    internal sealed class GrassBuffers : IDisposable
    {
        public readonly ComputeBuffer blades;
        public readonly ComputeBuffer generated;
        public readonly ComputeBuffer[] visible = new ComputeBuffer[4];
        public readonly GraphicsBuffer args;
        public readonly MaterialPropertyBlock[] properties = new MaterialPropertyBlock[4];
        public readonly GraphicsBuffer.IndirectDrawIndexedArgs[] commands = new GraphicsBuffer.IndirectDrawIndexedArgs[4];
        public readonly int capacity;
        public int version;
        public bool readbackPending;
        public uint generatedCount, visibleCount;
        public static readonly int CountOffset = FindCountOffset();
        static int FindCountOffset()
        {
            // Unity 6.6 exposes properties over platform-specific backing data, not public fields.
            var command = new GraphicsBuffer.IndirectDrawIndexedArgs { instanceCount = 0x1234abcd };
            IntPtr memory = Marshal.AllocHGlobal(GraphicsBuffer.IndirectDrawIndexedArgs.size);
            try
            {
                Marshal.StructureToPtr(command, memory, false);
                for (int i = 0; i <= GraphicsBuffer.IndirectDrawIndexedArgs.size - 4; i += 4)
                    if (Marshal.ReadInt32(memory, i) == 0x1234abcd) return i;
                throw new NotSupportedException("Cannot locate indirect instance count on this graphics platform.");
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        public long Bytes => capacity * 48L + 4 + 4 * GraphicsBuffer.IndirectDrawIndexedArgs.size;

        public GrassBuffers(int count, Mesh[] meshes)
        {
            capacity = count;
            blades = new ComputeBuffer(count, 32, ComputeBufferType.Structured);
            generated = new ComputeBuffer(1, 4, ComputeBufferType.Raw);
            args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 4, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            for (int i = 0; i < 4; i++)
            {
                visible[i] = new ComputeBuffer(count, 4, ComputeBufferType.Append);
                properties[i] = new MaterialPropertyBlock();
                properties[i].SetBuffer("_GrassBlades", blades);
                properties[i].SetBuffer("_GrassVisible", visible[i]);
                Mesh mesh = meshes[i == 3 ? 0 : i];
                commands[i] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = mesh.GetIndexCount(0),
                    startIndex = mesh.GetIndexStart(0),
                    baseVertexIndex = mesh.GetBaseVertex(0)
                };
            }
            args.SetData(commands);
        }
        public void Dispose()
        {
            blades.Release(); generated.Release(); args.Release();
            for (int i = 0; i < 4; i++) visible[i].Release();
        }
    }

    internal sealed class GrassBufferPool : IDisposable
    {
        readonly Stack<GrassBuffers> free = new Stack<GrassBuffers>(128);
        readonly List<GrassBuffers> all = new List<GrassBuffers>(128);
        readonly int capacity;
        readonly Mesh[] meshes;
        public int Leased { get; private set; }
        public long Bytes => all.Count * (capacity * 48L + 4 + 4 * GraphicsBuffer.IndirectDrawIndexedArgs.size);
        public GrassBufferPool(int capacity, Mesh[] meshes) { this.capacity = capacity; this.meshes = meshes; }
        public GrassBuffers Rent()
        {
            GrassBuffers b;
            if (free.Count != 0) b = free.Pop();
            else { b = new GrassBuffers(capacity, meshes); all.Add(b); }
            b.version++; b.generatedCount = 0; b.visibleCount = 0;
            Leased++;
            return b;
        }
        public void Return(GrassBuffers b) { b.version++; free.Push(b); Leased--; }
        public void Dispose() { foreach (GrassBuffers b in all) b.Dispose(); all.Clear(); free.Clear(); Leased = 0; }
    }
}
