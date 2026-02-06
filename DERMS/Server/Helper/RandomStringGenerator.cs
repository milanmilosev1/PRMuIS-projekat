using System;
using System.Linq;

public class RandomStringGenerator
{
    // Use a static Random instance to avoid generating the same sequence in quick succession
    private static readonly Random random = new Random();
    private const string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string GenerateRandomString(int length)
    {
        // Use LINQ for a concise implementation
        var chars = Enumerable.Repeat(AllowedChars, length)
                              .Select(s => s[random.Next(s.Length)])
                              .ToArray();
        return new string(chars);
    }
}
