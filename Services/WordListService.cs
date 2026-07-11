using System.IO;
using System.Text;

namespace ProfanityFilterEditor.Services;

public enum WordListFormat
{
    /// <summary>Whole file is one Base64 blob decoding to newline-separated words.</summary>
    WholeFileBase64,
    /// <summary>Each line is independently Base64-encoded.</summary>
    PerLineBase64,
    /// <summary>File is already plain, newline-separated text.</summary>
    PlainText
}

/// <summary>
/// Reads and writes profanity_filter.wlist. Mojang doesn't document this file's format and it
/// has changed shape across versions, so on load we detect which of the three known shapes
/// the file is in, and write back using that same shape so the game can still read it.
/// </summary>
public class WordListService
{
    public string FilePath { get; }
    public WordListFormat DetectedFormat { get; private set; } = WordListFormat.PlainText;

    public WordListService(string filePath)
    {
        FilePath = filePath;
    }

    public List<string> Load()
    {
        var bytes = File.ReadAllBytes(FilePath);
        var raw = new UTF8Encoding(false).GetString(bytes).Replace("\uFEFF", "");

        // 1) Try: whole file is a single Base64 blob.
        if (TryBase64Decode(raw.Trim(), out var wholeDecoded))
        {
            var lines = SplitLines(wholeDecoded);
            if (LooksLikeWordList(lines))
            {
                DetectedFormat = WordListFormat.WholeFileBase64;
                return lines;
            }
        }

        // 2) Try: each line is independently Base64-encoded.
        var rawLines = SplitLines(raw);
        if (rawLines.Count > 0)
        {
            var decodedLines = new List<string>();
            int successCount = 0;
            foreach (var line in rawLines)
            {
                if (TryBase64Decode(line, out var dec) && !dec.Contains('\uFFFD') && dec.Length > 0)
                {
                    decodedLines.Add(dec);
                    successCount++;
                }
                else
                {
                    decodedLines.Add(line);
                }
            }

            if (successCount >= rawLines.Count * 0.8)
            {
                DetectedFormat = WordListFormat.PerLineBase64;
                return decodedLines;
            }
        }

        // 3) Fall back: treat as plain text already.
        DetectedFormat = WordListFormat.PlainText;
        return rawLines;
    }

    public void Save(IEnumerable<string> words)
    {
        var wordList = words
            .Select(w => w.Trim())
            .Where(w => w.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(w => w, StringComparer.OrdinalIgnoreCase)
            .ToList();

        BackupIfNeeded();

        string output = DetectedFormat switch
        {
            WordListFormat.WholeFileBase64 =>
                Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join("\n", wordList))),

            WordListFormat.PerLineBase64 =>
                string.Join("\n", wordList.Select(w => Convert.ToBase64String(Encoding.UTF8.GetBytes(w)))),

            _ => string.Join("\n", wordList),
        };

        File.WriteAllText(FilePath, output, new UTF8Encoding(false));
    }

    private void BackupIfNeeded()
    {
        try
        {
            var backupPath = FilePath + ".bak";
            if (File.Exists(FilePath) && !File.Exists(backupPath))
            {
                File.Copy(FilePath, backupPath);
            }
        }
        catch
        {
            // Best-effort backup only; don't block saving because of it.
        }
    }

    private static bool TryBase64Decode(string input, out string decoded)
    {
        decoded = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        foreach (var c in input)
        {
            bool isBase64Char = char.IsLetterOrDigit(c) || c is '+' or '/' or '=' or '\r' or '\n';
            if (!isBase64Char) return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(input);
            decoded = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }

    private static bool LooksLikeWordList(List<string> lines)
    {
        if (lines.Count == 0) return false;
        int printable = lines.Count(l => l.All(c => !char.IsControl(c)));
        return printable >= lines.Count * 0.9;
    }
}
