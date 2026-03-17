namespace StreamApp.ViewModels
{
	public class EpisodeItemViewModel
	{
		// Removed 'required' and added default empty strings
		public string FileId { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string? StreamUrl { get; set; }
	}
}