#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

using Random = UnityEngine.Random;

namespace Kanna.Protecc
{
    public static class KannaProteccMesh
    {
        private static int GetSubmeshIndexForVertex(Mesh mesh, int vertexIndex)
        {
            // This gets the number of submeshes in the mesh
            var submeshCount = mesh.subMeshCount;

            // This variable will store the submesh index for that vertex, or -1 if not found
            var submeshIndex = -1;

            // This loop goes over all the submeshes
            for (var i = 0; i < submeshCount; i++)
            {
                // This gets the submesh at index i
                var submesh = mesh.GetSubMesh(i);

                // This checks if the vertex index is within the range of indices for that submesh
                if (vertexIndex >= submesh.firstVertex && vertexIndex < submesh.firstVertex + submesh.vertexCount)
                {
                    // If yes, then store the submesh index and break the loop
                    submeshIndex = i;
                    break;
                }
            }

            // This returns the result
            return submeshIndex;
        }

        public static Mesh EncryptMesh(Renderer renderer, Mesh mesh, float distortRatio, KannaProteccData data, List<Material> IgnoredMaterials)
        {
            if (mesh == null) return null;

            KannaLogger.LogToFile($"Encrypting Mesh: {mesh.name} On Renderer: {renderer.name}", KannaProteccRoot.LogLocation);

            var newVertices = mesh.vertices;
            var normals = mesh.normals;
            var uv7Offsets = new Vector2[mesh.vertexCount];
            var uv8Offsets = new Vector2[mesh.vertexCount];

            var maxDistance = mesh.bounds.max.magnitude - mesh.bounds.min.magnitude;

            var minRange = maxDistance * -distortRatio;
            const float maxRange = 0;

            for (var v = 0; v < newVertices.Length; v++)
            {
                var SubIndex = GetSubmeshIndexForVertex(mesh, v);
                
                if (renderer.sharedMaterials.Length > (SubIndex + 1))
                {
                    Debug.LogError($"Ignoring Mesh: {mesh.name} - SubMeshIndex Higher Than Amount Of Materials Available!");
                    SubIndex = renderer.sharedMaterials.Length - 1;
                }
                
                var mat = renderer.sharedMaterials[SubIndex];

                if (mat == null || !mat.shader.name.StartsWith("Kanna Protecc"))
                {
                    continue;
                }

                uv7Offsets[v].x = Random.Range(minRange, maxRange);
                uv7Offsets[v].y = Random.Range(minRange, maxRange);

                uv8Offsets[v].x = Random.Range(minRange, maxRange);
                uv8Offsets[v].y = Random.Range(minRange, maxRange);

                var isY = false;

                for (var i = 0; i < (data.ComKey.Length) / 2; i++) // theoretically should be exactly 50% of the length
                {
                    newVertices[v] += normals[v] * ((!isY ? uv7Offsets[v].x : uv7Offsets[v].y) * data.ComKey[i]);
                    isY = !isY;
                }

                isY = !isY;

                for (var i = (data.ComKey.Length) / 2; i < data.ComKey.Length; i++) // theoretically should be exactly after 50%
                {
                    newVertices[v] += normals[v] * ((!isY ? uv8Offsets[v].x : uv8Offsets[v].y) * data.ComKey[i]);
                    isY = !isY;
                }

                //newVertices[v] += normals[v] * (uv7Offsets[v].x * data.ComKey[0]);
                //newVertices[v] += normals[v] * (uv7Offsets[v].y * data.ComKey[1]);
                //newVertices[v] += normals[v] * (uv7Offsets[v].x * data.ComKey[2]);
                //newVertices[v] += normals[v] * (uv7Offsets[v].y * data.ComKey[3]);

                //newVertices[v] += normals[v] * (uv8Offsets[v].y * data.ComKey[4]);
                //newVertices[v] += normals[v] * (uv8Offsets[v].x * data.ComKey[5]);
                //newVertices[v] += normals[v] * (uv8Offsets[v].y * data.ComKey[6]);
                //newVertices[v] += normals[v] * (uv8Offsets[v].x * data.ComKey[7]);
            }

            var existingMeshPath = AssetDatabase.GetAssetPath(mesh);

            if (string.IsNullOrEmpty(existingMeshPath) || existingMeshPath.Contains("unity default resources"))
            {
                KannaLogger.LogToFile($"Asset For Mesh Not Found, Invalid Or Is A Built In Unity Mesh! -> {mesh.name}: {existingMeshPath ?? ""}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Warning);
                return null;
            }

            KannaLogger.LogToFile($"Existing Mesh Path For {mesh.name} Is {existingMeshPath}", KannaProteccRoot.LogLocation);

            //Do Not Care What File Type The Mesh Is, Attempt Anyway.
            //The Inline If Statement Is A Fallback Check, It Gets The Path Combined With The Filename Without Extension With Our Own Extension, If The Path Is Null, It Would Then Use Enviroment.CurrentDirectory Via Inheritance As The Path.
            //var encryptedMeshPath = Path.GetDirectoryName(existingMeshPath) != null
            //    ? (Path.Combine(Path.GetDirectoryName(existingMeshPath),
            //        Path.GetFileNameWithoutExtension(existingMeshPath)) + $"_{mesh.name}_Encrypted.asset")
            //    : (Path.GetFileNameWithoutExtension(existingMeshPath) + $"_{mesh.name}_Encrypted.asset");

            KannaLogger.LogToFile($"Creating Encrypted Mesh..", KannaProteccRoot.LogLocation);

            var encryptedMeshPath = Path.GetDirectoryName(existingMeshPath) != null
                ? (Path.Combine(Path.GetDirectoryName(existingMeshPath),
                    $"{GUID.Generate()}.asset"))
                : $"{GUID.Generate()}.asset";

            KannaLogger.LogToFile($"Encrypted Mesh Path: {encryptedMeshPath}", KannaProteccRoot.LogLocation);

            var newMesh = new Mesh
            {
                subMeshCount = mesh.subMeshCount,
                vertices = newVertices,
                colors = mesh.colors,
                normals = mesh.normals,
                tangents = mesh.tangents,
                bindposes = mesh.bindposes,
                boneWeights = mesh.boneWeights,
                indexFormat = mesh.indexFormat,
                uv = mesh.uv,
                uv2 = mesh.uv2,
                uv3 = mesh.uv3,
                uv4 = mesh.uv4,
                uv5 = mesh.uv5,
                uv6 = mesh.uv6,
                uv7 = uv7Offsets,
                uv8 = uv8Offsets,
                bounds = mesh.bounds,
                name = mesh.name,
                colors32 = mesh.colors32,
            };

            // transfer sub meshes
            for (var meshIndex = 0; meshIndex < mesh.subMeshCount; meshIndex++)
            {
                try
                {
                    var triangles = mesh.GetTriangles(meshIndex);

                    newMesh.SetTriangles(triangles, meshIndex);
                }
                catch (Exception e)
                {
                    KannaLogger.LogToFile($"Failed To Transfer Triangles For Mesh: {mesh.name}, {mesh.subMeshCount} != {newMesh.subMeshCount} Somehow. Error: {e}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Warning);
                }
            }

            KannaLogger.LogToFile($"Done, Transferring Blend Shapes..", KannaProteccRoot.LogLocation);

            // transfer blend shapes
            for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
            {
                for (var frameIndex = 0; frameIndex < mesh.GetBlendShapeFrameCount(shapeIndex); frameIndex++)
                {
                    var deltaVertices = new Vector3[newVertices.Length];
                    var deltaNormals = new Vector3[newVertices.Length];
                    var deltaTangents = new Vector3[newVertices.Length];
                    mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                    var weight = mesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                    var shapeName = mesh.GetBlendShapeName(shapeIndex);
                    newMesh.AddBlendShapeFrame(shapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                }
            }

            KannaLogger.LogToFile($"Done, Creating Mesh Asset File And Saving Assets..", KannaProteccRoot.LogLocation);

            AssetDatabase.CreateAsset(newMesh, encryptedMeshPath);
            AssetDatabase.SaveAssets();

            return newMesh;
        }
    }
}
#endif
