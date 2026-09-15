using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Net
{
    public sealed class FileAuthSessionStore : IAuthSessionStore
    {
        public const string FileName = "auth.json";

        private const string UidField = "uid";
        private const string RefreshTokenField = "refresh_token";

        private readonly string directory;

        public FileAuthSessionStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Auth directory is required.", nameof(directory));
            }

            this.directory = directory;
        }

        public string FilePath => Path.Combine(directory, FileName);

        public bool TryLoad(out AuthSession session)
        {
            session = null;
            if (!File.Exists(FilePath) || !JsonFields.TryParseObject(File.ReadAllText(FilePath), out var json))
            {
                return false;
            }

            var uid = JsonFields.Text(json, UidField);
            var refreshToken = JsonFields.Text(json, RefreshTokenField);
            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(refreshToken))
            {
                return false;
            }

            session = new AuthSession(uid, refreshToken);
            return true;
        }

        public void Save(AuthSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            Directory.CreateDirectory(directory);
            var json = new JObject { [UidField] = session.Uid, [RefreshTokenField] = session.RefreshToken };
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, json.ToString(Formatting.None));
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            File.Move(temporary, FilePath);
        }

        public void Clear()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }
}
