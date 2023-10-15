#if UNITY_EDITOR

using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Translator
{
    internal class Languages
    {
        internal static readonly string Bulgarian = "bg";
        internal static readonly string ChineseSimplified = "zh";
        internal static readonly string Czech = "cs";
        internal static readonly string Danish = "da";
        internal static readonly string Dutch = "nl";
        internal static readonly string Estonian = "et";
        internal static readonly string Finnish = "fi";
        internal static readonly string French = "fr";
        internal static readonly string German = "de";

        internal static readonly string Greek = "el";
        internal static readonly string Hungarian = "hu";
        internal static readonly string Indonesian = "id";
        internal static readonly string Italian = "it";
        internal static readonly string Japanese = "ja";
        internal static readonly string Korean = "ko";
        internal static readonly string Latvian = "lv";
        internal static readonly string Lithuanian = "lt";
        internal static readonly string Norwegian = "nb";
        internal static readonly string Polish = "pl";
        internal static readonly string Portuguese = "pt-PT";

        internal static readonly string PortugueseBrazilian = "pt-BR";
        internal static readonly string Romanian = "ro";
        internal static readonly string Russian = "ru";
        internal static readonly string Slovak = "sk";
        internal static readonly string Slovenian = "sl";
        internal static readonly string Spanish = "es";
        internal static readonly string Swedish = "sv";
        internal static readonly string Turkish = "tr";
        internal static readonly string Ukrainian = "uk";
    }

    public class TranslationResult
    {
        public string translated_text;
        public string error;
    }

    public class TranslationRequest
    {
        public string text;
        public string lang;
    }

    internal static HttpClient client = new HttpClient();

    internal static async Task<TranslationResult> TranslateText(string Text, string Language)
    {
        var result = await client.PostAsync("http://127.0.0.1:5000/translate", new StringContent(JsonConvert.SerializeObject(new TranslationRequest { text = Text, lang = Language }), Encoding.UTF8, "application/json"));

        TranslationResult output = null;

        try
        {
            output = JsonConvert.DeserializeObject<TranslationResult>(await result.Content.ReadAsStringAsync());
        }
        catch
        {
        }

        return output;
    }

}


#endif