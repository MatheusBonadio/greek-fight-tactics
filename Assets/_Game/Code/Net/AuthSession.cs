using System;

namespace Game.Net
{
    public sealed class AuthSession : IEquatable<AuthSession>
    {
        public AuthSession(string uid, string refreshToken)
        {
            Uid = uid;
            RefreshToken = refreshToken;
        }

        public string Uid { get; }

        public string RefreshToken { get; }

        public bool Equals(AuthSession other) =>
            other != null && string.Equals(Uid, other.Uid, StringComparison.Ordinal) && string.Equals(RefreshToken, other.RefreshToken, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as AuthSession);

        public override int GetHashCode() => HashCode.Combine(Uid, RefreshToken);

        public override string ToString() => $"AuthSession({Uid})";
    }

    public interface IAuthSessionStore
    {
        bool TryLoad(out AuthSession session);

        void Save(AuthSession session);

        void Clear();
    }
}
