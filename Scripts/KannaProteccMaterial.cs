#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Kanna.Protecc
{
    public class KannaDynamicShaderData
    {
        public class KannaReplaceText
        {
            public string[] TextToFind;
            public string TextToReplaceWith;
            public bool ApplyToIncludes;
            public string[] ExcludeIncludes = {};
        }

        public string[] ShaderName_StartsWith;
        public string[] FileContentsRegexMatch;

        public KannaReplaceText UV;
        public KannaReplaceText Vert;
        public KannaReplaceText VertexSetup;
    }

    public static class KannaProteccMaterial
    {
        public static string GenerateDecodeShader(KannaProteccData data, bool[] keys)
        {
            // Set this because someone from russia was getting ,'s in their decimals instead of .'s
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");

            ModelShaderDecodeFirst = "";

            for (var i = 0; i < keys.Length; i++) // Add bitKey fields
            {
                ModelShaderDecodeFirst += $"float _BitKey{i};\r\n";
            }

            ModelShaderDecodeFirst += "\r\nfloat4 modelDecode(float4 vertex, float3 normal, float2 uv0, float2 uv1)\r\n{\r\n    // KannaProtecc Randomly Generated Begin\r\n"; // Finish Off First Part

            ModelShaderDecodeSecond = "    // KannaProtecc Randomly Generated End\r\n\r\n";

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

            ModelShaderDecodeSecond += "\r\n    return vertex;\r\n}\r\n";

            var decodeKeys = new float[data.DividedCount];
            
            for (var i = 0; i < data.DividedCount; ++i)
            {
                decodeKeys[i] = (Convert.ToSingle(keys[i*KannaProteccData.CountDivisor]) + data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor]) *
                                (Convert.ToSingle(keys[i*KannaProteccData.CountDivisor+1]) + data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+1]) *
                                (Convert.ToSingle(keys[i*KannaProteccData.CountDivisor+2]) + data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+2]) *
                                (Convert.ToSingle(keys[i*KannaProteccData.CountDivisor+3]) + data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+3]);
            }

            var sb0 = new StringBuilder();
            var sb1 = new StringBuilder();
            for (var i = 0; i < data.DividedCount; ++i)
            {
                var firstAdd = data.KeySign0[i] > 0
                    ? decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor]] - decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+1]]
                    : decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor]] + decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+1]];
                var firstSign = data.Sign0[i] > 0 ? Mathf.Sin(firstAdd) : Mathf.Cos(firstAdd);

                var secondAdd = data.KeySign1[i] > 0
                    ? decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+2]] - decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+3]]
                    : decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+2]] + decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+3]];
                var secondSign = data.Sign1[i] > 0 ? Mathf.Sin(secondAdd) : Mathf.Cos(secondAdd);

                data.ComKey[i] = firstSign * data.RandomDivideMultiplier[i] * secondSign;

                var firstAddStr = data.KeySign0[i] > 0 ? "-" : "+";
                var firstSignStr = data.Sign0[i] > 0 ? "sin" : "cos";
                var secondAddStr = data.KeySign1[i] > 0 ? "-" : "+";
                var secondSignStr = data.Sign1[i] > 0 ? "sin" : "cos";

                sb0.AppendLine($"    float decodeKey{i} = (_BitKey{i*KannaProteccData.CountDivisor} + {data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor]}) * (_BitKey{i*KannaProteccData.CountDivisor+1} + {data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+1]}) * (_BitKey{i*KannaProteccData.CountDivisor+2} + {data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+2]}) * (_BitKey{i*KannaProteccData.CountDivisor+3} + {data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+3]});");

                sb1.AppendLine($"    float comKey{i} = {firstSignStr}(decodeKey{data.RandomKeyIndex[i*KannaProteccData.CountDivisor]} {firstAddStr} decodeKey{data.RandomKeyIndex[i*KannaProteccData.CountDivisor+1]}) * {data.RandomDivideMultiplier[i]} * {secondSignStr}(decodeKey{data.RandomKeyIndex[i*KannaProteccData.CountDivisor+2]} {secondAddStr} decodeKey{data.RandomKeyIndex[i*KannaProteccData.CountDivisor+3]});");
            }

            var decodeShader = $"{ModelDecodeIfndef}{ModelShaderDecodeFirst}{sb0}\r\n{sb1}{ModelShaderDecodeSecond}{ModelDecodeEndif}";
            return decodeShader;
        }

        public static bool IsShaderSupported(Shader shader, out KannaDynamicShaderData shaderData)
        {
            shaderData = Shaders.FirstOrDefault(o => o.ShaderName_StartsWith.Any(p => shader.name.Replace("Hidden/Locked/", "").StartsWith(p)) || (o.FileContentsRegexMatch?.Length > 0 && o.FileContentsRegexMatch.Any(p => Regex.IsMatch(File.ReadAllText(AssetDatabase.GetAssetPath(shader)), p)))   );

            if (shaderData == null)
            {
                KannaLogger.LogToFile($"Shader Unsupported: {shader.name}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Warning);
            }
            
            return shaderData != null;
        }

        private static readonly List<KannaDynamicShaderData> Shaders = new List<KannaDynamicShaderData>
        {
            new KannaDynamicShaderData
            {
                ShaderName_StartsWith = new [] { ".poiyomi/Poiyomi 7.3", ".poiyomi/Old Versions/7.3" },

                UV = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "float2 uv3 : TEXCOORD3;", "float2 uv3: TEXCOORD3;" },
                    TextToReplaceWith = "{OrigText}\r\nfloat3 uv6: TEXCOORD6;\r\nfloat3 uv7: TEXCOORD7;",
                    ApplyToIncludes = true,
                },

                Vert = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "v2f vert(", "v2f vert (", "V2FShadow vertShadowCaster(" },
                    TextToReplaceWith = "#include \"KannaModelDecode.cginc\"\r\n{OrigText}",
                    ApplyToIncludes = true,
                },

                VertexSetup = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "UNITY_SETUP_INSTANCE_ID(v);" },
                    TextToReplaceWith = "v.vertex = modelDecode(v.vertex, v.normal, v.uv6, v.uv7);\r\n{OrigText}",
                    ApplyToIncludes = true,
                }
            },

            new KannaDynamicShaderData
            {
                ShaderName_StartsWith = new [] { ".poiyomi/Poiyomi 8.0", ".poiyomi/Old Versions/8.0" },

                UV = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "float2 uv3 : TEXCOORD3;", "float2 uv3: TEXCOORD3;" },
                    TextToReplaceWith = "{OrigText}\r\nfloat3 uv6: TEXCOORD6;\r\nfloat3 uv7: TEXCOORD7;",
                    ApplyToIncludes = true,
                },

                Vert = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "v2f vert(", "v2f vert (", "V2FShadow vertShadowCaster(" },
                    TextToReplaceWith = "#include \"KannaModelDecode.cginc\"\r\n{OrigText}",
                    ApplyToIncludes = true,
                },

                VertexSetup = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "UNITY_SETUP_INSTANCE_ID(v);" },
                    TextToReplaceWith = "v.vertex = modelDecode(v.vertex, v.normal, v.uv6, v.uv7);\r\n{OrigText}",
                    ApplyToIncludes = true,
                }
            },

            new KannaDynamicShaderData
            {
                ShaderName_StartsWith = new [] { ".poiyomi/Poiyomi 8.1", ".poiyomi/Old Versions/8.1" },

                UV = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "float2 uv3 : TEXCOORD3;", "float2 uv3: TEXCOORD3;" },
                    TextToReplaceWith = "{OrigText}\r\nfloat3 uv6: TEXCOORD6;\r\nfloat3 uv7: TEXCOORD7;",
                    ApplyToIncludes = true,
                },

                Vert = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "VertexOut vert(", "VertexOut vert (", "V2FShadow vertShadowCaster(" },
                    TextToReplaceWith = "#include \"KannaModelDecode.cginc\"\r\n{OrigText}",
                    ApplyToIncludes = true,
                },

                VertexSetup = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "UNITY_SETUP_INSTANCE_ID(v);" },
                    TextToReplaceWith = "v.vertex = modelDecode(v.vertex, v.normal, v.uv6, v.uv7);\r\n{OrigText}",
                    ApplyToIncludes = true,
                }
            },

            new KannaDynamicShaderData
            {
                ShaderName_StartsWith = new [] { ".poiyomi/Poiyomi 8.2", ".poiyomi/Old Versions/8.2" },
                FileContentsRegexMatch = new [] { @"shader_master_label.*Poiyomi 8\.2", @"shader_master_label.*Poiyomi 9\.0", @"shader_master_label.*Poiyomi 9\.1", @"shader_master_label.*Poiyomi 9\.2" },

                UV = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "float2 uv3 : TEXCOORD3;", "float2 uv3: TEXCOORD3;" },
                    TextToReplaceWith = "{OrigText}\r\nfloat3 uv6: TEXCOORD6;\r\nfloat3 uv7: TEXCOORD7;",
                    ApplyToIncludes = true,
                },

                Vert = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "VertexOut vert(", "VertexOut vert (", "V2FShadow vertShadowCaster(" },
                    TextToReplaceWith = "#include \"KannaModelDecode.cginc\"\r\n{OrigText}",
                    ApplyToIncludes = true,
                },

                VertexSetup = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "UNITY_SETUP_INSTANCE_ID(v);" },
                    TextToReplaceWith = "v.vertex = modelDecode(v.vertex, v.normal, v.uv6, v.uv7);\r\n{OrigText}",
                    ApplyToIncludes = true,
                }
            },

            new KannaDynamicShaderData
            {
                ShaderName_StartsWith = new [] { "UnityChanToonShader" },

                UV = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "float3 bitangentDir : TEXCOORD3;" },
                    TextToReplaceWith = "{OrigText}\r\nfloat3 uv6: TEXCOORD6;\r\nfloat3 uv7: TEXCOORD7;",
                    ApplyToIncludes = true,
                },

                Vert = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "VertexOutput vert(", "VertexOutput vert (" },
                    TextToReplaceWith = "#include \"KannaModelDecode.cginc\"\r\n{OrigText}",
                    ApplyToIncludes = true
                },

                VertexSetup = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "UNITY_SETUP_INSTANCE_ID(v);" },
                    TextToReplaceWith = "v.vertex = modelDecode(v.vertex, v.normal, v.uv6, v.uv7);\r\n{OrigText}",
                    ApplyToIncludes = true,
                }
            },

            new KannaDynamicShaderData
            {
                ShaderName_StartsWith = new [] { "GeoTetra/GTAvaToon" }, // why you gotta have inconsistent ass syntax and naming rygo xD

                UV = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "float4 vertex : POSITION0;", "float4 vertex : POSITION;" },
                    TextToReplaceWith = "{OrigText}\r\nfloat3 uv6: TEXCOORD6;\r\nfloat3 uv7: TEXCOORD7;",
                    ApplyToIncludes = true,
                    ExcludeIncludes = new [] { "VRChatShadow" }
                },

                Vert = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "v2f vert(", "v2f vert (", "grabpass_v2f grabpass_vert" }, 
                    TextToReplaceWith = "#include \"KannaModelDecode.cginc\"\r\n{OrigText}",
                    ApplyToIncludes = true,
                    ExcludeIncludes = new [] { "VRChatShadow" }
                },

                VertexSetup = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "UNITY_TRANSFER_LIGHTING(o, v.uv1);", "o.normal_depth.w = _DepthId;" },
                    TextToReplaceWith = "{OrigText}\r\no.pos = modelDecode(o.pos, v.normal, v.uv6, v.uv7);",
                    ApplyToIncludes = true,
                    ExcludeIncludes = new [] { "VRChatShadow" }
                }
            },

            new KannaDynamicShaderData
            {
                ShaderName_StartsWith = new [] { "Sunao Shader" },

                UV = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "float2 uv1     : TEXCOORD1;", "float2 uv          : TEXCOORD0;" },
                    TextToReplaceWith = "{OrigText}\r\nfloat3 uv6: TEXCOORD6;\r\nfloat3 uv7: TEXCOORD7;",
                    ApplyToIncludes = true,
                },

                Vert = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "VOUT vert(", "VOUT vert (" },
                    TextToReplaceWith = "#include \"KannaModelDecode.cginc\"\r\n{OrigText}",
                    ApplyToIncludes = true
                },

                VertexSetup = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "UNITY_SETUP_INSTANCE_ID(v);" },
                    TextToReplaceWith = "v.vertex = modelDecode(v.vertex, v.normal, v.uv6, v.uv7);\r\n{OrigText}",
                    ApplyToIncludes = true,
                }
            },

            new KannaDynamicShaderData
            {
                ShaderName_StartsWith = new [] { "Xiexe/Toon" },

                UV = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "float2 uv2 : TEXCOORD2;" },
                    TextToReplaceWith = "{OrigText}\r\nfloat3 uv6: TEXCOORD6;\r\nfloat3 uv7: TEXCOORD7;",
                    ApplyToIncludes = true,
                },

                Vert = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "VertexOutput vert" },
                    TextToReplaceWith = "#include \"KannaModelDecode.cginc\"\r\n{OrigText}",
                    ApplyToIncludes = true
                },

                VertexSetup = new KannaDynamicShaderData.KannaReplaceText
                {
                    TextToFind = new [] { "UNITY_SETUP_INSTANCE_ID(v);" },
                    TextToReplaceWith = "v.vertex = modelDecode(v.vertex, v.normal, v.uv6, v.uv7);\r\n{OrigText}",
                    ApplyToIncludes = true,
                }
            }
        };

        private const string ModelDecodeIfndef = "#ifndef KANNAMODELDECODE\r\n#define KANNAMODELDECODE\r\n";

        private const string ModelDecodeEndif = "#endif\r\n";

        private static string ModelShaderDecodeFirst = "";

        // 1/2 divided length to each uv, total comKey count is 
        private static string ModelShaderDecodeSecond = "";
    }

    public class KannaProteccData
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
            using var provider = new RNGCryptoServiceProvider();
            var n = list.Count;
            while (n > 1)
            {
                var box = new byte[1];
                do provider.GetBytes(box);
                while (!(box[0] < n * (byte.MaxValue / n)));
                var k = (box[0] % n);
                n--;
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
        
        public KannaProteccData(int count)
        {
            DividedCount = count / CountDivisor;
            Sign0 = new int[DividedCount];

            var  randomKeyIndexList = new List<int>();
            
            KeySign0 = new int[DividedCount];
            Sign1 = new int[DividedCount];
            KeySign1 = new int[DividedCount];
            RandomDivideMultiplier = new float[DividedCount];
            RandomKeyMultiplier = new float[count];
            ComKey = new float[DividedCount];

            for (var i = 0; i < DividedCount; ++i)
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

            for (var i = 0; i < count; ++i)
            {
                RandomKeyMultiplier[i] = Random.Range(0f, 2f);
            }
        }
    }
}
#endif
