using AnimeStreamer.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StreamApp.Services
{
    public static class JikanService
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly Dictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

        // Smart Rate Limiting
        private static DateTime _lastApiCallTime = DateTime.MinValue;
        private const int JikanRateLimitMs = 400;

        static JikanService()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public static async Task<string?> GetCoverUrlAsync(string rawName)
        {
            // 1. Check Memory Cache
            if (_cache.TryGetValue(rawName, out var cached)) return cached;

            // 2. Check Hard Drive Cache
            string? localFilePath = await ImageCacheService.GetCachedImagePathIfExistsAsync(rawName);
            if (!string.IsNullOrEmpty(localFilePath))
            {
                _cache[rawName] = localFilePath;
                return localFilePath;
            }

            // 3. Query Jikan API with intelligent fallbacks
            foreach (var candidate in GetSearchCandidates(rawName))
            {
                // Respect API Rate Limits
                var timeSinceLastCall = (DateTime.UtcNow - _lastApiCallTime).TotalMilliseconds;
                if (timeSinceLastCall < JikanRateLimitMs)
                {
                    await Task.Delay(JikanRateLimitMs - (int)timeSinceLastCall);
                }

                System.Diagnostics.Debug.WriteLine($"[Jikan] Searching: '{candidate}'");
                var webUrl = await QueryJikanAsync(candidate);
                _lastApiCallTime = DateTime.UtcNow;

                if (webUrl != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Jikan] SUCCESS! Found cover for: '{candidate}'");
                    string localFileUrl = await ImageCacheService.GetCachedImageAsync(rawName, webUrl);
                    _cache[rawName] = localFileUrl;
                    return localFileUrl;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[Jikan] FAILED to find any match for: '{rawName}'");
            _cache[rawName] = null;
            return null;
        }

        private static IEnumerable<string> GetSearchCandidates(string name)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Try the exact original name
            if (seen.Add(name)) yield return name;

            string s = name;

            // 2. The Quote Extractor
            // Extracts core titles hidden in LN quotes like "Mugen Gacha"
            var quoteMatches = Regex.Matches(s, @"[""“”「」『』](.*?)[""“”「」『』]");
            foreach (Match match in quoteMatches)
            {
                string quoted = match.Groups[1].Value.Trim();
                if (seen.Add(quoted) && quoted.Length > 2) yield return quoted;
            }

            // 3. Strip out trailing years like (2024)
            s = Regex.Replace(s, @"\s*[\(\[]\d{4}[\)\]]\s*", " ").Trim();
            if (seen.Add(s) && s.Length > 0) yield return s;

            // 4. CRITICAL FIX: Strip Arc Subtitles Safely
            // Only split if the hyphen/colon has spaces around it! This protects "Nakama-tachi"
            var arcMatch = Regex.Match(s, @"\s+[-:~]\s+");
            if (arcMatch.Success)
            {
                s = s.Substring(0, arcMatch.Index).Trim();
                if (seen.Add(s) && s.Length > 0) yield return s;
            }

            // 5. Strip Season tags
            s = Regex.Replace(s, @"\s*(Season\s*\d+|\d+(st|nd|rd|th)\s*Season|S\d+|Part\s*\d+|Cour\s*\d+)\s*$", "", RegexOptions.IgnoreCase).Trim();
            if (seen.Add(s) && s.Length > 0) yield return s;

            // 6. Clean out weird punctuation
            var clean = Regex.Replace(s, @"[^\w\s]", " ");
            clean = Regex.Replace(clean, @"\s{2,}", " ").Trim();
            if (seen.Add(clean) && clean.Length > 0) yield return clean;

            // 7. Progressive Word Truncation (For insanely long Light Novel titles)
            var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length > 8)
            {
                var eightWords = string.Join(" ", words.Take(8));
                if (seen.Add(eightWords) && eightWords.Length > 0) yield return eightWords;
            }

            if (words.Length > 4)
            {
                var fourWords = string.Join(" ", words.Take(4));
                if (seen.Add(fourWords) && fourWords.Length > 0) yield return fourWords;
            }

            if (words.Length > 2)
            {
                var twoWords = string.Join(" ", words.Take(2));
                if (seen.Add(twoWords) && twoWords.Length > 0) yield return twoWords;
            }
        }

        private static async Task<string?> QueryJikanAsync(string name)
        {
            try
            {
                var q = Uri.EscapeDataString(name);

                // CRITICAL FIX: Fetch the top 5 results instead of just 1
                var json = await _http.GetStringAsync($"https://api.jikan.moe/v4/anime?q={q}&limit=5");

                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");

                if (data.GetArrayLength() == 0) return null;

                // 1. SMART FILTER: Check the top 5 results for an EXACT string match first!
                foreach (var item in data.EnumerateArray())
                {
                    string? mainTitle = item.GetProperty("title").GetString();

                    // Safely check for English titles (some MAL entries don't have them)
                    string? englishTitle = item.TryGetProperty("title_english", out var engProp) && engProp.ValueKind == JsonValueKind.String
                        ? engProp.GetString()
                        : null;

                    if (string.Equals(mainTitle, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(englishTitle, name, StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Jikan] Exact match found for: '{name}'");
                        return item.GetProperty("images").GetProperty("jpg").GetProperty("large_image_url").GetString();
                    }
                }

                // 2. FALLBACK: If no exact match is found, just grab the #1 result like before
                return data[0].GetProperty("images").GetProperty("jpg").GetProperty("large_image_url").GetString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Jikan API Error] {ex.Message}");
                return null;
            }
        }
    }
}