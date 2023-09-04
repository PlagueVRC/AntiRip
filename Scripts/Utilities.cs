#if UNITY_EDITOR
using UnityEditor;

public class Utilities
{
    private static int Length = 1;

    public static string GenerateRandomUniqueName(bool guid)
    {
        var str = "Kanna" + (guid ? $"_{GUID.Generate()}" : "");

        if (!guid)
        {
            str = str.PadRight(Length, '\u200B'); // \u200B = ZWSP

            Length++;
        }

        return str;
    }

    public static void ResetRandomizer()
    {
        Length = 1;
    }
}
#endif