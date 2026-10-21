using System.Text;
using Chess_Engine_2DArray_C_;
using ChessMove = Chess_Engine_2DArray_C_.Move; // Form already has a "Move" event

namespace ChessGui
{
    // Human vs engine. The engine searches on its own Board copy in a background task,
    // so the UI board is only ever touched on the UI thread.
    internal sealed class MainForm : Form
    {
        private readonly Board board = new Board();
        private readonly List<Move> moves = new List<Move>();
        private readonly List<string> sanMoves = new List<string>();
        private readonly TranspositionTable tt = new TranspositionTable(64);
        private readonly Sounds sounds = new Sounds();

        private bool humanWhite = true;
        private bool gameOver;
        private Task? engineTask;
        private Search? engineSearch;
        private int generation; // bumped whenever an in-flight engine result should be ignored

        private readonly BoardControl boardControl = new BoardControl { Dock = DockStyle.Fill };
        private readonly Label statusLabel = new Label();
        private readonly Label evalLabel = new Label();
        private readonly TextBox moveList = new TextBox();
        private readonly NumericUpDown thinkTime = new NumericUpDown();
        private readonly ContextMenuStrip promotionMenu = new ContextMenuStrip();
        private List<Move> pendingPromotions = new List<Move>();

        public MainForm()
        {
            Text = "Chess Engine 2DArray C#";
            ClientSize = new Size(1000, 720);
            MinimumSize = new Size(700, 520);
            BackColor = Color.FromArgb(48, 46, 43);
            Font = new Font("Segoe UI", 10f);

            Controls.Add(boardControl);
            Controls.Add(BuildSidePanel());

            boardControl.Board = board;
            boardControl.SquareClicked += OnSquareClicked;

            foreach (var (name, type) in new[] { ("Queen", Pieces.Queen), ("Rook", Pieces.Rook), ("Bishop", Pieces.Bishop), ("Knight", Pieces.Knight) })
                promotionMenu.Items.Add(name, null, (_, _) => ChoosePromotion(type));

            FormClosing += (_, _) => { StopEngine(); sounds.Dispose(); };
            StartNewGame(true);
        }

        private Control BuildSidePanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 280,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(38, 36, 33),
                ForeColor = Color.Gainsboro,
            };

            Button MakeButton(string text, EventHandler onClick)
            {
                var b = new Button
                {
                    Text = text,
                    Width = 250,
                    Height = 36,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(70, 68, 64),
                    ForeColor = Color.White,
                    Margin = new Padding(0, 0, 0, 6),
                };
                b.FlatAppearance.BorderColor = Color.FromArgb(90, 88, 84);
                b.Click += onClick;
                return b;
            }

            panel.Controls.Add(MakeButton("New game as White", (_, _) => StartNewGame(true)));
            panel.Controls.Add(MakeButton("New game as Black", (_, _) => StartNewGame(false)));
            panel.Controls.Add(MakeButton("Take back move", (_, _) => TakeBack()));
            panel.Controls.Add(MakeButton("Flip board", (_, _) => { boardControl.Flipped = !boardControl.Flipped; boardControl.Invalidate(); }));
            panel.Controls.Add(MakeButton("Copy FEN", (_, _) => Clipboard.SetText(board.ToFen())));

            var soundToggle = new CheckBox { Text = "Sound", Checked = true, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            soundToggle.CheckedChanged += (_, _) => sounds.Enabled = soundToggle.Checked;
            panel.Controls.Add(soundToggle);

            panel.Controls.Add(new Label { Text = "Engine think time (seconds):", AutoSize = true, Margin = new Padding(0, 10, 0, 2) });
            thinkTime.Minimum = 0.1m;
            thinkTime.Maximum = 60m;
            thinkTime.DecimalPlaces = 1;
            thinkTime.Increment = 0.5m;
            thinkTime.Value = 1m;
            thinkTime.Width = 250;
            panel.Controls.Add(thinkTime);

            statusLabel.AutoSize = false;
            statusLabel.Size = new Size(250, 48);
            statusLabel.Margin = new Padding(0, 14, 0, 0);
            statusLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            statusLabel.ForeColor = Color.White;
            panel.Controls.Add(statusLabel);

            evalLabel.AutoSize = false;
            evalLabel.Size = new Size(250, 44);
            panel.Controls.Add(evalLabel);

            moveList.Multiline = true;
            moveList.ReadOnly = true;
            moveList.ScrollBars = ScrollBars.Vertical;
            moveList.Size = new Size(250, 300);
            moveList.BackColor = Color.FromArgb(30, 29, 27);
            moveList.ForeColor = Color.Gainsboro;
            moveList.BorderStyle = BorderStyle.FixedSingle;
            moveList.Font = new Font("Consolas", 10f);
            panel.Controls.Add(moveList);

            return panel;
        }

        // GAME FLOW

        private void StartNewGame(bool playWhite)
        {
            StopEngine();
            board.LoadFromFen(Board.StartFen);
            moves.Clear();
            sanMoves.Clear();
            tt.Clear();
            humanWhite = playWhite;
            gameOver = false;
            boardControl.Flipped = !playWhite;
            boardControl.LastMove = default;
            boardControl.StopAnimation();
            evalLabel.Text = "";
            ClearSelection();
            RefreshUi();
            sounds.GameStartOrEnd();
            MaybeStartEngine();
        }

        private void OnSquareClicked(int sq)
        {
            if (gameOver || engineTask != null || board.WhiteToMove != humanWhite) return;

            Piece clicked = board.Squares[sq];
            bool ownPiece = clicked != Piece.Empty && Pieces.IsWhite(clicked) == humanWhite;

            if (boardControl.SelectedSquare >= 0 && boardControl.TargetSquares.Contains(sq))
            {
                var candidates = MoveGenerator.GenerateLegalMoves(board)
                    .Where(m => m.From == boardControl.SelectedSquare && m.To == sq)
                    .ToList();
                if (candidates.Count > 1)
                {
                    pendingPromotions = candidates;
                    promotionMenu.Show(Cursor.Position);
                    return;
                }
                PlayMove(candidates[0]);
                return;
            }

            ClearSelection();
            if (ownPiece)
            {
                boardControl.SelectedSquare = sq;
                foreach (Move m in MoveGenerator.GenerateLegalMoves(board))
                    if (m.From == sq) boardControl.TargetSquares.Add(m.To);
            }
            boardControl.Invalidate();
        }

        private void ChoosePromotion(int type)
        {
            Piece promo = Pieces.Make(type, humanWhite);
            Move move = pendingPromotions.FirstOrDefault(m => m.Promotion == promo);
            pendingPromotions = new List<Move>();
            if (!move.IsNone) PlayMove(move);
        }

        private void PlayMove(Move move)
        {
            bool capture = board.Squares[move.To] != Piece.Empty || move.Flag == ChessMove.FlagEnPassant;
            sanMoves.Add(San.ToSan(board, move));
            board.MakeMove(move);
            moves.Add(move);
            boardControl.LastMove = move;
            boardControl.AnimateMove(move);
            ClearSelection();
            RefreshUi();

            if (gameOver) sounds.GameStartOrEnd();
            else if (capture) sounds.Capture();
            else sounds.Move();

            if (!gameOver) MaybeStartEngine();
        }

        private void MaybeStartEngine()
        {
            if (gameOver || board.WhiteToMove == humanWhite) return;

            int myGeneration = generation;
            bool engineWhite = !humanWhite;

            // Replay the game on a separate board so the search keeps the full history for repetitions.
            var searchBoard = new Board();
            foreach (Move m in moves) searchBoard.MakeMove(m);
            var search = new Search(searchBoard, tt);
            search.Info = line => PostToUi(() => { if (myGeneration == generation) ShowInfo(line, engineWhite); });
            engineSearch = search;

            long ms = (long)(thinkTime.Value * 1000);
            var limits = new SearchLimits { SoftTimeMs = ms, HardTimeMs = ms };
            statusLabel.Text = "Engine is thinking...";

            engineTask = Task.Run(() =>
            {
                Move best = search.Think(limits, out _);
                PostToUi(() =>
                {
                    if (myGeneration != generation) return;
                    engineTask = null;
                    engineSearch = null;
                    if (!best.IsNone) PlayMove(best);
                });
            });
        }

        private void StopEngine()
        {
            generation++;
            if (engineTask == null) return;
            engineSearch?.Stop();
            engineTask.Wait();
            engineTask = null;
            engineSearch = null;
        }

        private void TakeBack()
        {
            StopEngine();
            if (moves.Count == 0) return;

            // Undo back to the most recent position where it is the human's turn.
            do
            {
                board.UnmakeMove();
                moves.RemoveAt(moves.Count - 1);
                sanMoves.RemoveAt(sanMoves.Count - 1);
            }
            while (moves.Count > 0 && board.WhiteToMove != humanWhite);

            gameOver = false;
            boardControl.LastMove = moves.Count > 0 ? moves[^1] : default;
            boardControl.StopAnimation();
            ClearSelection();
            RefreshUi();
            MaybeStartEngine(); // only moves if the human is Black at the very start
        }

        // DISPLAY

        private void RefreshUi()
        {
            gameOver = true;
            if (MoveGenerator.GenerateLegalMoves(board).Count == 0)
                statusLabel.Text = board.InCheck()
                    ? $"Checkmate - {(board.WhiteToMove ? "Black" : "White")} wins"
                    : "Stalemate - draw";
            else if (board.RepetitionCount() >= 3)
                statusLabel.Text = "Draw by threefold repetition";
            else if (board.HalfmoveClock >= 100)
                statusLabel.Text = "Draw by 50-move rule";
            else if (board.IsInsufficientMaterial())
                statusLabel.Text = "Draw - insufficient material";
            else
            {
                gameOver = false;
                bool humanTurn = board.WhiteToMove == humanWhite;
                statusLabel.Text = (humanTurn ? "Your move" : "Engine to move") + (board.InCheck() ? " (check!)" : "");
            }

            var sb = new StringBuilder();
            for (int i = 0; i < sanMoves.Count; i += 2)
            {
                sb.Append($"{i / 2 + 1,3}. {sanMoves[i],-8}");
                if (i + 1 < sanMoves.Count) sb.Append(sanMoves[i + 1]);
                sb.AppendLine();
            }
            moveList.Text = sb.ToString();
            moveList.SelectionStart = moveList.TextLength;
            moveList.ScrollToCaret();

            boardControl.Invalidate();
        }

        private void ShowInfo(string line, bool engineWhite)
        {
            // "info depth 12 score cp 35 nodes ... pv ..."; score is from the engine's side.
            string[] t = line.Split(' ');
            int depthIdx = Array.IndexOf(t, "depth"), scoreIdx = Array.IndexOf(t, "score");
            if (depthIdx < 0 || scoreIdx < 0 || scoreIdx + 2 >= t.Length) return;

            int value = int.Parse(t[scoreIdx + 2]);
            if (!engineWhite) value = -value;
            string score = t[scoreIdx + 1] == "mate"
                ? $"mate in {Math.Abs(value)} for {(value > 0 ? "White" : "Black")}"
                : $"{value / 100.0:+0.00;-0.00;0.00}";
            evalLabel.Text = $"Engine eval: {score}\n(depth {t[depthIdx + 1]}, + = good for White)";
        }

        private void ClearSelection()
        {
            boardControl.SelectedSquare = -1;
            boardControl.TargetSquares.Clear();
        }

        private void PostToUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // The window closed while the engine was still reporting.
            }
        }
    }
}
