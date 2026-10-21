namespace Chess_Engine_2DArray_C_
{
    // Hash table of previously searched positions, keyed by the Zobrist key.
    public sealed class TranspositionTable
    {
        public const byte Exact = 0, LowerBound = 1, UpperBound = 2;

        private struct Entry
        {
            public ulong Key;
            public int Move;
            public short Score;
            public sbyte Depth;
            public byte Flag;
        }

        private Entry[] table = Array.Empty<Entry>();
        private ulong mask;

        public TranspositionTable(int megabytes)
        {
            Resize(megabytes);
        }

        public void Resize(int megabytes)
        {
            long bytes = Math.Max(1, megabytes) * 1024L * 1024L;
            long count = 1;
            while (count * 2 * 16 <= bytes) count *= 2; // 16 bytes per entry, power-of-two size
            table = new Entry[count];
            mask = (ulong)count - 1;
        }

        public void Clear()
        {
            Array.Clear(table);
        }

        public bool Probe(ulong key, out Move move, out int score, out int depth, out byte flag)
        {
            ref Entry e = ref table[key & mask];
            if (e.Key == key)
            {
                move = new Move(e.Move);
                score = e.Score;
                depth = e.Depth;
                flag = e.Flag;
                return true;
            }
            move = Move.None;
            score = depth = 0;
            flag = 0;
            return false;
        }

        public void Store(ulong key, Move move, int score, int depth, byte flag)
        {
            ref Entry e = ref table[key & mask];
            // Keep the old best move if this result has none (e.g. a fail-low).
            if (move.IsNone && e.Key == key) move = new Move(e.Move);
            e.Key = key;
            e.Move = move.Value;
            e.Score = (short)score;
            e.Depth = (sbyte)Math.Clamp(depth, -128, 127);
            e.Flag = flag;
        }
    }
}
