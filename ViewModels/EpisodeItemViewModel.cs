namespace StreamApp.ViewModels
{
	public class EpisodeItemViewModel
	{
		// Removed 'required' and added default empty strings
		public string FileId { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string? StreamUrl { get; set; }
        // NEW: Stores the data for the next episode in the season
        public EpisodeItemViewModel? NextEpisode { get; set; }
    }
}