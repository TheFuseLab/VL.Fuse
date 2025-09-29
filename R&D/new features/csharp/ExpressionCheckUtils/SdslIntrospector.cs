using System.Text.RegularExpressions;

namespace Fuse.AutoPins;

using System.Text;
using System.Text.RegularExpressions;

public static class SdslIntrospector
{
    public static Signature Parse(string filePath, string function, bool verbose = false)
    {
        void Log(string s)
        {
            if (verbose) Console.WriteLine("[SdslIntrospector] " + s);
        }

        string NormalizeFuncName(string f)
        {
            var m = Regex.Match(f ?? "", @"^[A-Za-z_]\w*");
            if (!m.Success) throw new Exception($"Invalid function id: '{f}'");
            return m.Value;
        }

        string text = File.ReadAllText(filePath);
        string func = NormalizeFuncName(function);

        Log($"File: {filePath}");
        Log($"Length: {text.Length} chars");
        Log($"Newlines: \\n={text.Count(c => c=='\\n')}, \\r={text.Count(c=>c=='\\r')}, U+2028={text.Count(c=>c=='\u2028')}, U+2029={text.Count(c=>c=='\u2029')}");
        Log($"Looking for function: '{func}'");

        // List all function candidates first
        var allFnRx = new Regex(@"(?m)^\s*(?:\[[^\]]*\]\s*)*(?:\w+\s+)*void\s+([A-Za-z_]\w*)\s*\(", RegexOptions.Multiline);
        var all = allFnRx.Matches(text).Cast<Match>().ToList();
        if (all.Count == 0) Log("No 'void <name>(' candidates found at line starts.");
        else
        {
            Log($"Found {all.Count} candidate function(s):");
            for (int i = 0; i < all.Count; i++)
            {
                var name = all[i].Groups[1].Value;
                (int line, int col) = GetLineCol(text, all[i].Index);
                Log($"  {i+1,2}. {name,-24} @ L{line}:C{col}");
            }
        }

        // Primary (anchored) pattern
        string anchoredPattern = $@"(?m)^\s*(?:\[[^\]]*\]\s*)*(?:\w+\s+)*void\s+{Regex.Escape(func)}\s*\(";
        Log($"Anchored pattern: {anchoredPattern}");
        var mAnchored = Regex.Match(text, anchoredPattern);
        Log($"Anchored match: {(mAnchored.Success ? "YES" : "NO")}");

        Match m = mAnchored;

        // Fallback: unanchored search (anywhere in file)
        if (!m.Success)
        {
            string anyPattern = $@"\bvoid\s+{Regex.Escape(func)}\s*\(";
            Log($"Fallback-anywhere pattern: {anyPattern}");
            var mAny = Regex.Match(text, anyPattern);
            Log($"Fallback-anywhere match: {(mAny.Success ? "YES" : "NO")}");
            if (mAny.Success) m = mAny;
        }

        // Fallback: plain string search just to show context
        if (!m.Success)
        {
            int idx = text.IndexOf(func + "(", StringComparison.Ordinal);
            Log($"Plain string IndexOf('{func}('): {(idx >= 0 ? $"FOUND at {idx}" : "NOT FOUND")}");
            if (idx >= 0)
            {
                Log(Context(text, Math.Max(0, idx - 80), 160));
            }

            var candidates = all.Select(mm => mm.Groups[1].Value).Distinct();
            throw new Exception($"Function '{func}' not found. Candidates: {string.Join(", ", candidates)}");
        }

        // Find the matching ')' for the parameter list
        int start = m.Index + m.Length;
        int depth = 1, iPos = start;
        for (; iPos < text.Length && depth > 0; iPos++)
        {
            char ch = text[iPos];
            if (ch == '(') depth++;
            else if (ch == ')') depth--;
        }
        if (depth != 0)
        {
            Log(Context(text, Math.Max(0, start - 40), 120));
            throw new Exception("Unbalanced parentheses in parameter list.");
        }

        string paramList = text.Substring(start, iPos - start - 1);
        Log("Parameter list:");
        Log(paramList.Replace("\r", "\\r").Replace("\n", "\\n\n"));

        // Parse params
        var ps = new List<Param>();
        var paramRx = new Regex(@"(?<dir>out|inout)?\s*(?<type>(?:\w+)(?:\d(?:x\d)?)?(?:<[^>]+>)?)\s+(?<name>[A-Za-z_]\w*)\s*(?:,|\))");
        foreach (Match pm in paramRx.Matches(paramList))
        {
            var dir = pm.Groups["dir"].Value switch
            {
                "out" => ParamDir.Out,
                "inout" => ParamDir.InOut,
                _ => ParamDir.In
            };
            var type = pm.Groups["type"].Value.Trim();
            var name = pm.Groups["name"].Value.Trim();
            ps.Add(new Param(name, type, dir, false, ClassifyResource(type)));
            Log($"  Param: {dir,-5} {type,-18} {name}");
        }

        // Collect includes from hints
        var includes = Regex.Matches(text, @"^\s*//\s*@include\s+(.*)$", RegexOptions.Multiline)
                            .Cast<Match>().Select(x => x.Groups[1].Value.Trim()).Distinct().ToArray();
        if (includes.Length > 0) Log("Include hints: " + string.Join(", ", includes));

        Log("SUCCESS.");
        return new Signature(func, ps, includes);
    }

    static (int line, int col) GetLineCol(string text, int index)
    {
        int line = 1, col = 1;
        for (int i = 0; i < index; i++)
        {
            if (text[i] == '\n') { line++; col = 1; }
            else col++;
        }
        return (line, col);
    }

    static string Context(string s, int start, int len)
    {
        int a = Math.Max(0, start);
        int b = Math.Min(s.Length, a + len);
        var frag = s.Substring(a, b - a).Replace("\r", "\\r").Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
        return "---- context ----\n" + frag + "\n-----------------";
    }

    static ResKind ClassifyResource(string type)
    {
        var t = type.Replace(" ", "").ToLowerInvariant();
        if (t.StartsWith("texture3d")) return ResKind.Texture3D;
        if (t.StartsWith("texture2d")) return ResKind.Texture2D;
        if (t.StartsWith("texture1d")) return ResKind.Texture1D;
        if (t.Contains("sampler")) return ResKind.Sampler;
        if (t.StartsWith("rwtexture2d")) return ResKind.RWTexture2D;
        if (t.StartsWith("rwtexture3d")) return ResKind.RWTexture3D;
        if (t.Contains("buffer")) return ResKind.Buffer;
        return ResKind.None;
    }
}
