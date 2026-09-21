using System;
using System.IO;
using Game.UI;

namespace Game.Tests.PlayMode
{
    internal sealed class IsolatedDataDirectory : IDisposable
    {
        private readonly string previous;

        private IsolatedDataDirectory(string directory)
        {
            Directory = directory;
            previous = Environment.GetEnvironmentVariable(GameServices.DataDirectoryVariable);
            Environment.SetEnvironmentVariable(GameServices.DataDirectoryVariable, directory);
        }

        public string Directory { get; }

        public static IsolatedDataDirectory Enter() =>
            new(Path.Combine(Path.GetTempPath(), $"gft-playmode-{Guid.NewGuid():N}"));

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(GameServices.DataDirectoryVariable, previous);
            try
            {
                if (System.IO.Directory.Exists(Directory))
                {
                    System.IO.Directory.Delete(Directory, true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
