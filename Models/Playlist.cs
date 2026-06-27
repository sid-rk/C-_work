using System;
using System.Collections.Generic;

namespace WindowsFormsApp1.Models
{
    public class Playlist
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<MusicFile> Tracks { get; set; } = new List<MusicFile>();
    }
}
