using System.Media;

namespace ChessGui
{
    // The lichess "standard" sound set, embedded as WAV resources and preloaded so playback is instant.
    internal sealed class Sounds : IDisposable
    {
        private readonly SoundPlayer? move = Load("Move");
        private readonly SoundPlayer? capture = Load("Capture");
        private readonly SoundPlayer? notify = Load("GenericNotify");

        public bool Enabled { get; set; } = true;

        public void Move() => Play(move);
        public void Capture() => Play(capture);
        public void GameStartOrEnd() => Play(notify);

        private void Play(SoundPlayer? player)
        {
            if (Enabled) player?.Play(); // asynchronous; a new sound cuts off the previous one
        }

        private static SoundPlayer? Load(string name)
        {
            Stream? stream = typeof(Sounds).Assembly.GetManifestResourceStream($"ChessGui.{name}.wav");
            if (stream == null) return null;
            var player = new SoundPlayer(stream);
            player.Load();
            return player;
        }

        public void Dispose()
        {
            move?.Dispose();
            capture?.Dispose();
            notify?.Dispose();
        }
    }
}
