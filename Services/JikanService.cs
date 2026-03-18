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
            // Make the User-Agent look like a standard browser to avoid 403 blocks
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public static async Task<string?> GetCoverUrlAsync(string rawName)
        {
            if (_cache.TryGetValue(rawName, out var cached)) return cached;

            foreach (var candidate in GetSearchCandidates(rawName))
            {
                var url = await QueryJikanAsync(candidate);
                if (url != null)
                {
                    _cache[rawName] = url;
                    return url;
                }
                // INCREASE DELAY: Jikan limits to 3 requests per second. 
                await Task.Delay(500);
            }

            _cache[rawName] = null;
            return null;
        }

        private static IEnumerable<string> GetSearchCandidates(string name)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            yield return name;

            var s = Regex.Replace(name, @"\s*\(\d{4}\)\s*$", "").Trim();
            if (seen.Add(s) && s.Length > 0) yield return s;

            s = Regex.Replace(s, @"\s*(Season\s*\d+|\d+(st|nd|rd|th)\s*Season|S\d+|Part\s*\d+|Cour\s*\d+)\s*$", "", RegexOptions.IgnoreCase).Trim();
            if (seen.Add(s) && s.Length > 0) yield return s;

            var clean = Regex.Replace(s, @"[^\w\s]", " ");
            clean = Regex.Replace(clean, @"\s{2,}", " ").Trim();
            if (seen.Add(clean) && clean.Length > 0) yield return clean;
        }

        private static async Task<string?> QueryJikanAsync(string name)
        {
            try
            {
                var q = Uri.EscapeDataString(name);
                var json = await _http.GetStringAsync($"https://api.jikan.moe/v4/anime?q={q}&limit=1");
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                if (data.GetArrayLength() == 0) return null;
                return data[0].GetProperty("images").GetProperty("jpg").GetProperty("image_url").GetString();
            }
            catch (HttpRequestException httpEx)
            {
                // NOW you will see exactly why it's failing in the Output window
                System.Diagnostics.Debug.WriteLine($"[Jikan Error] {httpEx.StatusCode}: {httpEx.Message}");
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}