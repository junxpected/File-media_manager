using System.IO;

namespace File_manager.Services
{
    // Окремий клас для дебаунсу FileSystemWatcher подій
    // Чекає 500мс після останньої події перед тим як передати її далі
    public class FswDebouncer
    {
        private readonly Dictionary<string, System.Timers.Timer> _timers = new();
        private readonly object _lock = new();
        private readonly int _delayMs;

        public FswDebouncer(int delayMs = 500)
        {
            _delayMs = delayMs;
        }

        public void Debounce(string key, Action callback)
        {
            lock (_lock)
            {
                if (_timers.TryGetValue(key, out var old))
                {
                    old.Stop();
                    old.Dispose();
                }

                var timer = new System.Timers.Timer(_delayMs) { AutoReset = false };
                timer.Elapsed += (_, __) =>
                {
                    lock (_lock)
                    {
                        if (_timers.TryGetValue(key, out var t))
                        {
                            t.Dispose();
                            _timers.Remove(key);
                        }
                    }
                    callback();
                };
                _timers[key] = timer;
                timer.Start();
            }
        }
    }
}