using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamApp.Services
{
    public static class JikanService
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly Dictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

        static JikanService()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "AnimeStreamerApp/1.0");
        }

        public static async Task<string?> GetCoverUrlAsync(string rawName)
        {
            if (_cache.TryGetValue(rawName, out var cached)) return cached;

            // Try multiple progressively-simplified versions of the name
            foreach (var candidate in GetSearchCandidates(rawName))
            {
                var url = await QueryJikanAsync(candidate);
                if (url != null)
                {
                    _cache[rawName] = url;
                    return url;
                }
                await Task.Delay(350); // Jikan rate limit between attempts
            }

            _cache[rawName] = null;
            return null;
        }

        private static IEnumerable<string> GetSearchCandidates(string name)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // void Yield(string s) { if (!string.IsNullOrWhiteSpace(s) && seen.Add(s)) ; }

            // 1. Raw name
            yield return name;

            // 2. Strip trailing year "(2021)"
            var s = Regex.Replace(name, @"\s*\(\d{4}\)\s*$", "").Trim();
            if (seen.Add(s) && s.Length > 0) yield return s;

            // 3. Strip season/part suffixes
            s = Regex.Replace(s, @"\s*(Season\s*\d+|\d+(st|nd|rd|th)\s*Season|S\d+|Part\s*\d+|Cour\s*\d+)\s*$",
                              "", RegexOptions.IgnoreCase).Trim();
            if (seen.Add(s) && s.Length > 0) yield return s;

            // 4. Remove non-alphanumeric characters (colons, dashes, etc.)
            var clean = Regex.Replace(s, @"[^\w\s]", " ");
            clean = Regex.Replace(clean, @"\s{2,}", " ").Trim();
            if (seen.Add(clean) && clean.Length > 0) yield return clean;

            // 5. First half of the name (up to first colon or dash)
            var colonIdx = name.IndexOfAny(new[] { ':', '-', '–' });
            if (colonIdx > 3)
            {
                var prefix = name[..colonIdx].Trim();
                if (seen.Add(prefix)) yield return prefix;
            }
        }

        private static async Task<string?> QueryJikanAsync(string name)
        {
            try
            {
                var q = Uri.EscapeDataString(name);
                var json = await _http.GetStringAsync(
                    $"https://api.jikan.moe/v4/anime?q={q}&limit=1");
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                if (data.GetArrayLength() == 0) return null;
                return data[0].GetProperty("images").GetProperty("jpg")
                              .GetProperty("image_url").GetString();
            }
            catch { return null; }
        }
    }
}
