using System.Text;

namespace OpenFmodBank.Core;

public static class BinarySearch
{
    public static int Find(byte[] source, byte[] pattern)
    {
        if (pattern.Length == 0) return 0;
        if (source.Length < pattern.Length) return -1;

        byte first = pattern[0];
        int limit = source.Length - pattern.Length;

        for (int i = 0; i <= limit; i++)
        {
            if (source[i] != first)
                continue;

            int j = pattern.Length - 1;
            while (j > 0 && source[i + j] == pattern[j])
                j--;

            if (j == 0)
                return i;
        }

        return -1;
    }

    public static int FindString(byte[] source, string text) =>
        Find(source, Encoding.ASCII.GetBytes(text));
}
