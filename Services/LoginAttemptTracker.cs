using System.Collections.Concurrent;

namespace Sales.Services
{
    public interface ILoginAttemptTracker
    {
        int Increment(string email);
        void Reset(string email);
        bool HasBeenNotified(string email);
        void MarkNotified(string email);
    }

    public class LoginAttemptTracker : ILoginAttemptTracker
    {
        private record Entry(int Count, bool Notified);

        private readonly ConcurrentDictionary<string, Entry> _store =
            new(StringComparer.OrdinalIgnoreCase);

        public int Increment(string email)
        {
            var updated = _store.AddOrUpdate(
                email,
                _ => new Entry(1, false),
                (_, prev) => prev with { Count = prev.Count + 1 });
            return updated.Count;
        }

        public void Reset(string email) => _store.TryRemove(email, out _);

        public bool HasBeenNotified(string email) =>
            _store.TryGetValue(email, out var e) && e.Notified;

        public void MarkNotified(string email) =>
            _store.AddOrUpdate(
                email,
                _ => new Entry(5, true),
                (_, prev) => prev with { Notified = true });
    }
}
