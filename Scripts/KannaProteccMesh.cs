#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Kanna.Protecc
{
    public static class KannaProteccMesh
    {
        public static Mesh EncryptMesh(Mesh mesh, float distortRatio, KannaProteccData data)
        {
            if (mesh == null) return null;

            Vector3[] newVertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uv7Offsets = new Vector2[mesh.vertexCount];
            Vector2[] uv8Offsets = new Vector2[mesh.vertexCount];

            float maxDistance = mesh.bounds.max.magnitude - mesh.bounds.min.magnitude;

            float minRange = maxDistance * -distortRatio;
            const float maxRange = 0;

            for (var v = 0; v < newVertices.Length; v++)
            {
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
                Debug.LogError("Asset For Mesh Not Found, Invalid Or Is A Built In Unity Mesh!");
                return null;
            }

            Debug.Log($"Existing Mesh Path For {mesh.name} Is {existingMeshPath}");

            //Do Not Care What File Type The Mesh Is, Attempt Anyway.
            //The Inline If Statement Is A Fallback Check, It Gets The Path Combined With The Filename Without Extension With Our Own Extension, If The Path Is Null, It Would Then Use Enviroment.CurrentDirectory Via Inheritance As The Path.
            var encryptedMeshPath = Path.GetDirectoryName(existingMeshPath) != null
                ? (Path.Combine(Path.GetDirectoryName(existingMeshPath),
                    Path.GetFileNameWithoutExtension(existingMeshPath)) + $"_{mesh.name}_Encrypted.asset")
                : (Path.GetFileNameWithoutExtension(existingMeshPath) + $"_{mesh.name}_Encrypted.asset");

            Debug.Log($"Encrypted Mesh Path {encryptedMeshPath}");

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
                uv8 = uv8Offsets
            };

            // transfer sub meshes
            for (var meshIndex = 0; meshIndex < mesh.subMeshCount; meshIndex++)
            {
                var triangles = mesh.GetTriangles(meshIndex);

                newMesh.SetTriangles(triangles, meshIndex);
            }

            // transfer blend shapes
            for (int shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
            {
                for (var frameIndex = 0; frameIndex < mesh.GetBlendShapeFrameCount(shapeIndex); frameIndex++)
                {
                    Vector3[] deltaVertices = new Vector3[newVertices.Length];
                    Vector3[] deltaNormals = new Vector3[newVertices.Length];
                    Vector3[] deltaTangents = new Vector3[newVertices.Length];
                    mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                    float weight = mesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                    string shapeName = mesh.GetBlendShapeName(shapeIndex);
                    newMesh.AddBlendShapeFrame(shapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                }
            }

            AssetDatabase.CreateAsset(newMesh, encryptedMeshPath);
            AssetDatabase.SaveAssets();

            return newMesh;
        }
    }
}
#endif
