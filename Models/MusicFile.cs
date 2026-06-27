using System;

namespace WindowsFormsApp1.Models
{
    public class MusicFile
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string FilePath { get; set; }
        public double Duration { get; set; }
        public string Format { get; set; }
        public long FileSize { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.Now;
        public int PlayCount { get; set; } = 0;

        public string DisplayName => string.IsNullOrEmpty(Artist)
            ? Title
            : $"{Artist} - {Title}";
    }
}
