using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utilities
{
    private static int Length = 1;

    public static string GenerateRandomUniqueName()
    {
        var str = "Kanna";

        str = str.PadRight(Length, '​');

        Length++;

        return str;
    }

    public static void ResetRandomizer()
    {
        Length = 1;
    }
}
