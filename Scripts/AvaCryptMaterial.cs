#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GeoTetra.GTAvaCrypt
{
    public static class AvaCryptMaterial
    {
        public static string GenerateDecodeShader(AvaCryptData data, bool[] keys)
        {
            // Set this because someone from russia was getting ,'s in their decimals instead of .'s
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");

            ModelShaderDecodeFirst = "";

            for (var i = 0; i < keys.Length; i++) // Add bitKey fields
            {
                ModelShaderDecodeFirst += $"float _BitKey{i};\r\n";
            }

            ModelShaderDecodeFirst += "\r\nfloat4 modelDecode(float4 vertex, float3 normal, float2 uv0, float2 uv1)\r\n{\r\n    // AvaCrypt Randomly Generated Begin\r\n"; // Finish Off First Part

            // Make ModelShaderDecodeSecond dynamic too, and make the comKey{i} based on a ideal count. Context: this is for ShaderLab.

            ModelShaderDecodeSecond = "    // AvaCrypt Randomly Generated End\r\n\r\n";

            var isY = false;

            for (var i = 0; i < (data.ComKey.Length) / 2; i++) // theoretically should be exactly 50% of the length
            {
                ModelShaderDecodeSecond += $"    vertex.xyz -= normal * (uv0.{(!isY ? "x" : "y")} * comKey{i});\r\n";
                isY = !isY;
            }

            isY = !isY;

            ModelShaderDecodeSecond += "\r\n";

            for (var i = (data.ComKey.Length) / 2; i < data.ComKey.Length; i++) // theoretically should be exactly after 50%
            {
                ModelShaderDecodeSecond += $"    vertex.xyz -= normal * (uv1.{(!isY ? "x" : "y")} * comKey{i});\r\n";
                isY = !isY;
            }

            ModelShaderDecodeSecond += "\r\n\r\n    return vertex;\r\n}\r\n";

            float[] decodeKeys = new float[data.DividedCount];
            
            for (int i = 0; i < data.DividedCount; ++i)
            {
                decodeKeys[i] = (Convert.ToSingle(keys[i*AvaCryptData.CountDivisor]) + data.RandomKeyMultiplier[i*AvaCryptData.CountDivisor]) *
                                (Convert.ToSingle(keys[i*AvaCryptData.CountDivisor+1]) + data.RandomKeyMultiplier[i*AvaCryptData.CountDivisor+1]) *
                                (Convert.ToSingle(keys[i*AvaCryptData.CountDivisor+2]) + data.RandomKeyMultiplier[i*AvaCryptData.CountDivisor+2]) *
                                (Convert.ToSingle(keys[i*AvaCryptData.CountDivisor+3]) + data.RandomKeyMultiplier[i*AvaCryptData.CountDivisor+3]);
                Debug.Log("decodeKey: " + decodeKeys[i]);
            }

            StringBuilder sb0 = new StringBuilder();
            StringBuilder sb1 = new StringBuilder();
            for (int i = 0; i < data.DividedCount; ++i)
            {
                float firstAdd = data.KeySign0[i] > 0
                    ? decodeKeys[data.RandomKeyIndex[i*AvaCryptData.CountDivisor]] - decodeKeys[data.RandomKeyIndex[i*AvaCryptData.CountDivisor+1]]
                    : decodeKeys[data.RandomKeyIndex[i*AvaCryptData.CountDivisor]] + decodeKeys[data.RandomKeyIndex[i*AvaCryptData.CountDivisor+1]];
                float firstSign = data.Sign0[i] > 0 ? Mathf.Sin(firstAdd) : Mathf.Cos(firstAdd);

                float secondAdd = data.KeySign1[i] > 0
                    ? decodeKeys[data.RandomKeyIndex[i*AvaCryptData.CountDivisor+2]] - decodeKeys[data.RandomKeyIndex[i*AvaCryptData.CountDivisor+3]]
                    : decodeKeys[data.RandomKeyIndex[i*AvaCryptData.CountDivisor+2]] + decodeKeys[data.RandomKeyIndex[i*AvaCryptData.CountDivisor+3]];
                float secondSign = data.Sign1[i] > 0 ? Mathf.Sin(secondAdd) : Mathf.Cos(secondAdd);

                data.ComKey[i] = firstSign * data.RandomDivideMultiplier[i] * secondSign;

                string firstAddStr = data.KeySign0[i] > 0 ? "-" : "+";
                string firstSignStr = data.Sign0[i] > 0 ? "sin" : "cos";
                string secondAddStr = data.KeySign1[i] > 0 ? "-" : "+";
                string secondSignStr = data.Sign1[i] > 0 ? "sin" : "cos";

                sb0.AppendLine($"    float decodeKey{i} = (_BitKey{i*AvaCryptData.CountDivisor} + {data.RandomKeyMultiplier[i*AvaCryptData.CountDivisor]}) * (_BitKey{i*AvaCryptData.CountDivisor+1} + {data.RandomKeyMultiplier[i*AvaCryptData.CountDivisor+1]}) * (_BitKey{i*AvaCryptData.CountDivisor+2} + {data.RandomKeyMultiplier[i*AvaCryptData.CountDivisor+2]}) * (_BitKey{i*AvaCryptData.CountDivisor+3} + {data.RandomKeyMultiplier[i*AvaCryptData.CountDivisor+3]});");

                sb1.AppendLine($"    float comKey{i} = {firstSignStr}(decodeKey{data.RandomKeyIndex[i*AvaCryptData.CountDivisor]} {firstAddStr} decodeKey{data.RandomKeyIndex[i*AvaCryptData.CountDivisor+1]}) * {data.RandomDivideMultiplier[i]} * {secondSignStr}(decodeKey{data.RandomKeyIndex[i*AvaCryptData.CountDivisor+2]} {secondAddStr} decodeKey{data.RandomKeyIndex[i*AvaCryptData.CountDivisor+3]});");
            }

            string decodeShader = $"{ModelDecodeIfndef}{ModelShaderDecodeFirst}{sb0}\r\n{sb1}{ModelShaderDecodeSecond}{ModelDecodeEndif}";
            return decodeShader;
        }
        
        public const string DefaultFallback = "\"VRCFallback\" = \"Standard\"";
        
        public const string AlteredFallback = "\"VRCFallback\" = \"Hidden\"";

        public const string DefaultPoiUV = "float2 uv3 : TEXCOORD3;";
        
        public const string AlteredPoiUV = "float2 uv3 : TEXCOORD3; float3 uv6: TEXCOORD6; float3 uv7: TEXCOORD7;";

        // public const string DefaultPoiUVArray = "float2 uv[4] : TEXCOORD0;";
        //
        // public const string AlteredPoiUVArray = "float2 uv[4] : TEXCOORD0; float2 avUv6 : AVAUV0; float2 avUv7 : AVAUV1;";
        
        public const string DefaultPoiVert = "v2f vert(";

        public const string NewDefaultPoiVert = "VertexOut vert(";

        public const string AlteredPoiVert = "#include \"GTModelDecode.cginc\"\nv2f vert(";

        public const string NewAlteredPoiVert = "#include \"GTModelDecode.cginc\"\nVertexOut vert(";

        public const string DefaultVertSetup = "UNITY_SETUP_INSTANCE_ID(v);";
        
        public const string AlteredVertSetup = "v.vertex = modelDecode(v.vertex, v.normal, v.uv6, v.uv7); UNITY_SETUP_INSTANCE_ID(v);";

        // public const string DefaultUvTransfer = "o.uv[3] = v.uv3;";
        //
        // public const string AlteredUvTransfer = "o.uv[3] = v.uv3; avUv6 = v.uv6; avUv7 = v.uv7;";

        public const string ModelDecodeIfndef = "#ifndef GTMODELDECODE\n#define GTMODELDECODE\n";
        
        public const string ModelDecodeEndif = "#endif\n";
        
        static string ModelShaderDecodeFirst = 
@"float _BitKey0;
float _BitKey1;
float _BitKey2;
float _BitKey3;

float _BitKey4;
float _BitKey5;
float _BitKey6;
float _BitKey7;

float _BitKey8;
float _BitKey9;
float _BitKey10;
float _BitKey11;

float _BitKey12;
float _BitKey13;
float _BitKey14;
float _BitKey15;

float _BitKey16;
float _BitKey17;
float _BitKey18;
float _BitKey19;

float _BitKey20;
float _BitKey21;
float _BitKey22;
float _BitKey23;

float _BitKey24;
float _BitKey25;
float _BitKey26;
float _BitKey27;

float _BitKey28;
float _BitKey29;
float _BitKey30;
float _BitKey31;

float4 modelDecode(float4 vertex, float3 normal, float2 uv0, float2 uv1)
{
    // AvaCrypt Randomly Generated Begin
";

        // 1/2 divided length to each uv, total comKey count is 
        static string ModelShaderDecodeSecond =
@" 
    // AvaCrypt Randomly Generated End

    vertex.xyz -= normal * (uv0.x * comKey0);
    vertex.xyz -= normal * (uv0.y * comKey1);
    vertex.xyz -= normal * (uv0.x * comKey2);
    vertex.xyz -= normal * (uv0.y * comKey3);

    vertex.xyz -= normal * (uv1.y * comKey4);
    vertex.xyz -= normal * (uv1.x * comKey5);
    vertex.xyz -= normal * (uv1.y * comKey6);
    vertex.xyz -= normal * (uv1.x * comKey7);

    return vertex;
}";
    }

    public class AvaCryptData
    {
        public const int CountDivisor = 4;

        public readonly int DividedCount;
        public readonly int[] Sign0;
        public readonly int[] KeySign0;
        public readonly int[] RandomKeyIndex;
        public readonly int[] Sign1;
        public readonly int[] KeySign1;
        public readonly float[] RandomDivideMultiplier;
        public readonly float[] RandomKeyMultiplier;
        public readonly float[] ComKey;

        void Shuffle<T>(IList<T> list)
        {
            RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
            int n = list.Count;
            while (n > 1)
            {
                byte[] box = new byte[1];
                do provider.GetBytes(box);
                while (!(box[0] < n * (Byte.MaxValue / n)));
                int k = (box[0] % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
        
        public AvaCryptData(int count)
        {
            DividedCount = count / CountDivisor;
            Sign0 = new int[DividedCount];

            List<int>  randomKeyIndexList = new List<int>();
            
            KeySign0 = new int[DividedCount];
            Sign1 = new int[DividedCount];
            KeySign1 = new int[DividedCount];
            RandomDivideMultiplier = new float[DividedCount];
            RandomKeyMultiplier = new float[count];
            ComKey = new float[DividedCount];

            for (int i = 0; i < DividedCount; ++i)
            {
                Sign0[i] = Random.Range(0, 2);
                KeySign0[i] = Random.Range(0, 2);
                Sign1[i] = Random.Range(0, 2);
                KeySign1[i] = Random.Range(0, 2);

                randomKeyIndexList.Add(i);
                randomKeyIndexList.Add(i);
                randomKeyIndexList.Add(i);
                randomKeyIndexList.Add(i);
                
                RandomDivideMultiplier[i] = Random.Range(0f, 2f);
            }
            
            Shuffle(randomKeyIndexList);
            RandomKeyIndex = randomKeyIndexList.ToArray();

            for (int i = 0; i < count; ++i)
            {
                RandomKeyMultiplier[i] = Random.Range(0f, 2f);
            }
        }
    }
}
#endif