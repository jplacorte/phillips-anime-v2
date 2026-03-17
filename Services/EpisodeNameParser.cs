using System.Text.RegularExpressions;

namespace StreamApp.Services
{
    public static class EpisodeNameParser
    {
        public static string FormatEpisodeName(string parentFolderName, int episodeNumber, bool isOva)
        {
            string title = string.IsNullOrWhiteSpace(parentFolderName) ? "Unknown Anime" : parentFolderName;
            string numStr = episodeNumber.ToString("D2");
            if (isOva)
            {
                return $"{title} - OVA{numStr}";
            }
            else
            {
                return $"{title} - E{numStr}";
            }
        }
    }
}
