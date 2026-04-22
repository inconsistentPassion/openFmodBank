using System.Text;

namespace OpenFmodBank.Core.Services;

/// <summary>
/// Fast byte-pattern search for locating headers in binary files.
/// </summary>
public static class BinarySearch
{
    /// <summary>
    /// Find the first occurrence of <paramref name="pattern"/> in <paramref name="source"/>.
    /// Returns -1 if not found.
    /// </summary>
    public static int Find(byte[] source, byte[] pattern)
    {
        if (pattern.Length == 0) return 0;
        if (source.Length < pattern.Length) return -1;

        // Build a simple skip table for the first byte
        byte first = pattern[0];
        int limit = source.Length - pattern.Length;

        for (int i = 0; i <= limit; i++)
        {
            if (source[i] != first)
                continue;

            // Check rest of pattern from end (fail fast on mismatches near the end)
            int j = pattern.Length - 1;
            while (j > 0 && source[i + j] == pattern[j])
                j--;

            if (j == 0)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Find all occurrences of <paramref name="pattern"/> in <paramref name="source"/>.
    /// </summary>
    public static IEnumerable<int> FindAll(byte[] source, byte[] pattern)
    {
        int offset = 0;
        while (offset <= source.Length - pattern.Length)
        {
            int found = Find(
                source[offset..],
                pattern);

            if (found < 0)
                yield break;

            yield return offset + found;
            offset += found + 1;
        }
    }

    /// <summary>
    /// Search for an ASCII string pattern.
    /// </summary>
    public static int FindString(byte[] source, string text) =>
        Find(source, Encoding.ASCII.GetBytes(text));
}
