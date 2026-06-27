using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Windows.Forms;
using WindowsFormsApp1.Models;

namespace WindowsFormsApp1.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            string dbPath = Path.Combine(Application.StartupPath, "MediaMate.db");
            _connectionString = $"Data Source={dbPath};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                string createMusicTable = @"
                    CREATE TABLE IF NOT EXISTS MusicFiles (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        Artist TEXT DEFAULT '',
                        Album TEXT DEFAULT '',
                        FilePath TEXT NOT NULL UNIQUE,
                        Duration REAL DEFAULT 0,
                        Format TEXT DEFAULT '',
                        FileSize INTEGER DEFAULT 0,
                        DateAdded DATETIME DEFAULT CURRENT_TIMESTAMP,
                        PlayCount INTEGER DEFAULT 0
                    )";

                string createPlaylistTable = @"
                    CREATE TABLE IF NOT EXISTS Playlists (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";

                string createPlaylistItemTable = @"
                    CREATE TABLE IF NOT EXISTS PlaylistItems (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        PlaylistId INTEGER NOT NULL,
                        MusicFileId INTEGER NOT NULL,
                        SortOrder INTEGER DEFAULT 0,
                        FOREIGN KEY(PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
                        FOREIGN KEY(MusicFileId) REFERENCES MusicFiles(Id) ON DELETE CASCADE
                    )";

                using (var cmd = new SQLiteCommand(createMusicTable, conn)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand(createPlaylistTable, conn)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand(createPlaylistItemTable, conn)) cmd.ExecuteNonQuery();
            }
        }

        // ========== MusicFiles CRUD ==========

        public void InsertMusicFile(MusicFile file)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                    INSERT OR IGNORE INTO MusicFiles (Title, Artist, Album, FilePath, Duration, Format, FileSize)
                    VALUES (@Title, @Artist, @Album, @FilePath, @Duration, @Format, @FileSize)";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", file.Title);
                    cmd.Parameters.AddWithValue("@Artist", file.Artist ?? "");
                    cmd.Parameters.AddWithValue("@Album", file.Album ?? "");
                    cmd.Parameters.AddWithValue("@FilePath", file.FilePath);
                    cmd.Parameters.AddWithValue("@Duration", file.Duration);
                    cmd.Parameters.AddWithValue("@Format", file.Format ?? "");
                    cmd.Parameters.AddWithValue("@FileSize", file.FileSize);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<MusicFile> GetAllMusicFiles()
        {
            var list = new List<MusicFile>();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT * FROM MusicFiles ORDER BY DateAdded DESC";

                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapToMusicFile(reader));
                    }
                }
            }
            return list;
        }

        public void DeleteMusicFile(int id)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("DELETE FROM MusicFiles WHERE Id=@Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void IncrementPlayCount(int id)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("UPDATE MusicFiles SET PlayCount=PlayCount+1 WHERE Id=@Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ========== Playlists CRUD ==========

        public int CreatePlaylist(string name)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("INSERT INTO Playlists (Name) VALUES (@Name); SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public List<Playlist> GetAllPlaylists()
        {
            var playlists = new List<Playlist>();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT * FROM Playlists ORDER BY CreatedAt DESC";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var playlist = new Playlist
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Name = reader["Name"].ToString(),
                            CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                        };
                        playlists.Add(playlist);
                    }
                }
            }
            return playlists;
        }

        public void DeletePlaylist(int id)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("DELETE FROM Playlists WHERE Id=@Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new SQLiteCommand("DELETE FROM PlaylistItems WHERE PlaylistId=@Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void AddTrackToPlaylist(int playlistId, int musicFileId, int sortOrder)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = "INSERT INTO PlaylistItems (PlaylistId, MusicFileId, SortOrder) VALUES (@p, @m, @s)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@p", playlistId);
                    cmd.Parameters.AddWithValue("@m", musicFileId);
                    cmd.Parameters.AddWithValue("@s", sortOrder);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RemoveTrackFromPlaylist(int playlistId, int musicFileId)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = "DELETE FROM PlaylistItems WHERE PlaylistId=@p AND MusicFileId=@m";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@p", playlistId);
                    cmd.Parameters.AddWithValue("@m", musicFileId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<MusicFile> GetTracksInPlaylist(int playlistId)
        {
            var list = new List<MusicFile>();
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                string sql = @"
                    SELECT m.* FROM MusicFiles m
                    JOIN PlaylistItems p ON m.Id = p.MusicFileId
                    WHERE p.PlaylistId = @Id
                    ORDER BY p.SortOrder";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", playlistId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(MapToMusicFile(reader));
                        }
                    }
                }
            }
            return list;
        }

        // ========== Helpers ==========

        private MusicFile MapToMusicFile(SQLiteDataReader reader)
        {
            return new MusicFile
            {
                Id = Convert.ToInt32(reader["Id"]),
                Title = reader["Title"].ToString(),
                Artist = reader["Artist"].ToString(),
                Album = reader["Album"].ToString(),
                FilePath = reader["FilePath"].ToString(),
                Duration = Convert.ToDouble(reader["Duration"]),
                Format = reader["Format"].ToString(),
                FileSize = Convert.ToInt64(reader["FileSize"]),
                DateAdded = Convert.ToDateTime(reader["DateAdded"]),
                PlayCount = Convert.ToInt32(reader["PlayCount"])
            };
        }
    }
}
