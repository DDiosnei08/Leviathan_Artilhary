using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    sealed class TerrainChunkGenerationRequest
    {
        public TerrainChunk Chunk { get; }
        public TerrainGenerationSettings Settings { get; }
        public Vector2 WorldOrigin { get; }
        public int Lod { get; }

        public TerrainChunkGenerationRequest(
            TerrainChunk chunk,
            TerrainGenerationSettings settings,
            Vector2 worldOrigin,
            int lod)
        {
            Chunk = chunk;
            Settings = settings;
            WorldOrigin = worldOrigin;
            Lod = lod;
        }
    }

    public class TerrainChunkGenerationResult
    {
        public TerrainChunk Chunk { get; }
        public MeshData MeshData { get; }
        public HeightMap TerrainData { get; }
        public int RequestedLod { get; }
        public System.Exception Failure { get; }

        public TerrainChunkGenerationResult(
            TerrainChunk chunk,
            MeshData meshData,
            HeightMap terrainData,
            int requestedLod,
            System.Exception failure = null)
        {
            Chunk = chunk;
            MeshData = meshData;
            TerrainData = terrainData;
            RequestedLod = requestedLod;
            Failure = failure;
        }
    }

    public class ThreadedTerrainGenerator : MonoBehaviour
    {
        readonly Queue<TerrainChunkGenerationRequest> pendingGeneration = new Queue<TerrainChunkGenerationRequest>();
        readonly Queue<TerrainChunkGenerationResult> completedGeneration = new Queue<TerrainChunkGenerationResult>();
        readonly HashSet<Vector2Int> queuedChunks = new HashSet<Vector2Int>();
        readonly object queueLock = new object();

        Thread workerThread;
        volatile bool keepRunning = true;

        public void RequestChunk(TerrainChunk chunk)
        {
            if (chunk == null || chunk.Settings == null)
            {
                return;
            }

            lock (queueLock)
            {
                if (queuedChunks.Contains(chunk.Coordinate))
                {
                    return;
                }

                TerrainGenerationSettings generationSettings = chunk.Settings.CaptureGenerationSettings();
                Vector2 worldOrigin = chunk.Settings.GetChunkWorldOrigin(chunk.Coordinate);
                pendingGeneration.Enqueue(new TerrainChunkGenerationRequest(
                    chunk,
                    generationSettings,
                    worldOrigin,
                    chunk.CurrentLod));
                queuedChunks.Add(chunk.Coordinate);
            }
        }

        public bool TryGetCompletedChunk(out TerrainChunkGenerationResult result)
        {
            lock (queueLock)
            {
                if (completedGeneration.Count == 0)
                {
                    result = null;
                    return false;
                }

                result = completedGeneration.Dequeue();
                queuedChunks.Remove(result.Chunk.Coordinate);
                return true;
            }
        }

        void OnEnable()
        {
            if (workerThread != null && workerThread.IsAlive)
            {
                return;
            }

            keepRunning = true;
            workerThread = new Thread(WorkerLoop);
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        void OnDisable()
        {
            keepRunning = false;
            if (workerThread != null && workerThread.IsAlive)
            {
                workerThread.Join();
            }

            workerThread = null;
        }

        void Update()
        {
            while (TryGetCompletedChunk(out var result))
            {
                if (result == null)
                {
                    continue;
                }

                if (result.Chunk != null && result.Chunk.GameObject != null &&
                    result.Chunk.CurrentLod != result.RequestedLod)
                {
                    if (result.Failure != null)
                    {
                        Debug.LogException(result.Failure, this);
                    }

                    RequestChunk(result.Chunk);
                    continue;
                }

                if (result.Failure != null)
                {
                    result.Chunk?.MarkGenerationFailed();
                    Debug.LogException(result.Failure, this);
                    continue;
                }

                if (result.Chunk != null && result.Chunk.GameObject != null)
                {
                    result.Chunk.ApplyGeneratedData(result.MeshData, result.TerrainData);
                }
            }
        }

        void WorkerLoop()
        {
            while (keepRunning)
            {
                TerrainChunkGenerationRequest request = null;
                lock (queueLock)
                {
                    if (pendingGeneration.Count > 0)
                    {
                        request = pendingGeneration.Dequeue();
                    }
                }

                if (request == null)
                {
                    Thread.Sleep(10);
                    continue;
                }

                try
                {
                    HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(
                        request.Settings,
                        request.Settings.chunkResolution,
                        request.Settings.chunkResolution,
                        request.WorldOrigin);
                    MeshData meshData = BaseTerrainMesh.Generate(heightMap, request.Settings, request.Lod);

                    lock (queueLock)
                    {
                        completedGeneration.Enqueue(new TerrainChunkGenerationResult(
                            request.Chunk,
                            meshData,
                            heightMap,
                            request.Lod));
                    }
                }
                catch (System.Exception exception)
                {
                    lock (queueLock)
                    {
                        completedGeneration.Enqueue(new TerrainChunkGenerationResult(
                            request.Chunk,
                            null,
                            null,
                            request.Lod,
                            exception));
                    }
                }
            }
        }
    }
}
