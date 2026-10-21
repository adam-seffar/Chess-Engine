using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using Chess_Engine_2DArray_C_;
using Svg;
using ChessMove = Chess_Engine_2DArray_C_.Move; // Control already has a "Move" event

namespace ChessGui
{
    // Draws the board and reports clicked squares. Holds no game logic.
    internal sealed class BoardControl : Control
    {
        private const int LabelMargin = 22; // room for the rank/file labels
        private const double AnimationMs = 200;

        private static readonly Color Light = Color.FromArgb(240, 217, 181);
        private static readonly Color Dark = Color.FromArgb(181, 136, 99);
        private static readonly Color LastMoveTint = Color.FromArgb(105, 155, 199, 0);
        private static readonly Color SelectedTint = Color.FromArgb(130, 20, 85, 30);
        private static readonly Color TargetDot = Color.FromArgb(80, 20, 85, 30);
        private static readonly Color CheckTint = Color.FromArgb(200, 255, 0, 0);

        // Vector piece images, rasterized again whenever the square size changes.
        private static readonly SvgDocument?[] PieceSvgs = LoadPieceSvgs();
        private readonly Bitmap?[] pieceImages = new Bitmap?[13];
        private int pieceImageSize;

        // Pieces currently sliding to their squares: (piece, from, to). They are hidden on their
        // destination square and drawn on top at an interpolated position instead.
        private readonly List<(Piece Piece, int From, int To)> sliding = new List<(Piece, int, int)>();
        private readonly Stopwatch animationClock = new Stopwatch();
        private readonly System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer { Interval = 10 };

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Board? Board { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Flipped { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedSquare { get; set; } = -1;
        public HashSet<int> TargetSquares { get; } = new HashSet<int>();
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ChessMove LastMove { get; set; }

        public event Action<int>? SquareClicked;

        public BoardControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.FromArgb(48, 46, 43);
            Cursor = Cursors.Hand;
            animationTimer.Tick += (_, _) =>
            {
                if (animationClock.Elapsed.TotalMilliseconds >= AnimationMs) StopAnimation();
                Invalidate();
            };
        }

        private static SvgDocument?[] LoadPieceSvgs()
        {
            var docs = new SvgDocument?[13];
            var assembly = typeof(BoardControl).Assembly;
            for (int p = 1; p <= 12; p++)
            {
                Piece piece = (Piece)p;
                string name = $"ChessGui.{(Pieces.IsWhite(piece) ? 'w' : 'b')}{char.ToUpper(Pieces.ToChar(piece))}.svg";
                using Stream? stream = assembly.GetManifestResourceStream(name);
                if (stream != null) docs[p] = SvgDocument.Open<SvgDocument>(stream);
            }
            return docs;
        }

        // Call right after the move has been made on the board.
        public void AnimateMove(ChessMove move)
        {
            if (Board == null) return;
            sliding.Clear();
            sliding.Add((Board.Squares[move.To], move.From, move.To));

            if (move.Flag == ChessMove.FlagCastle)
            {
                bool kingside = (move.To & 7) == 6;
                int rank = move.To & ~7;
                int rookFrom = rank + (kingside ? 7 : 0), rookTo = rank + (kingside ? 5 : 3);
                sliding.Add((Board.Squares[rookTo], rookFrom, rookTo));
            }

            animationClock.Restart();
            animationTimer.Start();
            Invalidate();
        }

        public void StopAnimation()
        {
            animationTimer.Stop();
            sliding.Clear();
            Invalidate();
        }

        private int SquareSize => Math.Max(8, (Math.Min(Width, Height) - 2 * LabelMargin) / 8);

        private Point Origin
        {
            get
            {
                int boardSize = SquareSize * 8;
                return new Point((Width - boardSize) / 2, (Height - boardSize) / 2);
            }
        }

        private Rectangle SquareRect(int sq)
        {
            int row = sq >> 3, col = sq & 7;
            if (Flipped)
            {
                row = 7 - row;
                col = 7 - col;
            }
            int s = SquareSize;
            return new Rectangle(Origin.X + col * s, Origin.Y + row * s, s, s);
        }

        private int SquareAt(Point p)
        {
            int s = SquareSize;
            int col = (p.X - Origin.X) / s, row = (p.Y - Origin.Y) / s;
            if (p.X < Origin.X || p.Y < Origin.Y || col > 7 || row > 7) return -1;
            if (Flipped)
            {
                row = 7 - row;
                col = 7 - col;
            }
            return row * 8 + col;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int sq = SquareAt(e.Location);
            if (sq >= 0 && e.Button == MouseButtons.Left) SquareClicked?.Invoke(sq);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Board == null) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            int s = SquareSize;
            EnsurePieceImages(s);

            int checkedKing = Board.InCheck() ? Board.KingSquare[Board.WhiteToMove ? 0 : 1] : -1;

            for (int sq = 0; sq < 64; sq++)
            {
                Rectangle r = SquareRect(sq);
                bool light = ((sq >> 3) + (sq & 7)) % 2 == 0;
                using (var b = new SolidBrush(light ? Light : Dark)) g.FillRectangle(b, r);

                if (!LastMove.IsNone && (sq == LastMove.From || sq == LastMove.To))
                    using (var b = new SolidBrush(LastMoveTint)) g.FillRectangle(b, r);
                if (sq == SelectedSquare)
                    using (var b = new SolidBrush(SelectedTint)) g.FillRectangle(b, r);
                if (sq == checkedKing)
                    DrawCheckGlow(g, r);

                Piece p = Board.Squares[sq];
                if (p != Piece.Empty && !sliding.Exists(a => a.To == sq)) DrawPiece(g, p, r);

                if (TargetSquares.Contains(sq))
                {
                    using var b = new SolidBrush(TargetDot);
                    if (p == Piece.Empty)
                    {
                        int d = s * 2 / 7;
                        g.FillEllipse(b, r.X + (s - d) / 2, r.Y + (s - d) / 2, d, d);
                    }
                    else
                    {
                        using var pen = new Pen(TargetDot, Math.Max(3, s / 12));
                        g.DrawEllipse(pen, r.X + 3, r.Y + 3, s - 6, s - 6);
                    }
                }
            }

            // Sliding pieces go on top of everything, eased in and out like lichess.
            if (sliding.Count > 0)
            {
                double t = Math.Min(1.0, animationClock.Elapsed.TotalMilliseconds / AnimationMs);
                double eased = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
                foreach (var (piece, from, to) in sliding)
                {
                    Rectangle a = SquareRect(from), b = SquareRect(to);
                    int x = (int)Math.Round(a.X + (b.X - a.X) * eased);
                    int y = (int)Math.Round(a.Y + (b.Y - a.Y) * eased);
                    DrawPiece(g, piece, new Rectangle(x, y, s, s));
                }
            }

            // Coordinates
            using var font = new Font("Segoe UI", Math.Max(7, s / 7f), FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb(200, 200, 200));
            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            for (int i = 0; i < 8; i++)
            {
                int file = Flipped ? 7 - i : i;
                int rank = Flipped ? i + 1 : 8 - i;
                g.DrawString(((char)('a' + file)).ToString(), font, brush,
                    new RectangleF(Origin.X + i * s, Origin.Y + 8 * s, s, LabelMargin), center);
                g.DrawString(rank.ToString(), font, brush,
                    new RectangleF(Origin.X - LabelMargin, Origin.Y + i * s, LabelMargin, s), center);
            }
        }

        private void EnsurePieceImages(int size)
        {
            if (size == pieceImageSize) return;
            for (int p = 1; p <= 12; p++)
            {
                pieceImages[p]?.Dispose();
                pieceImages[p] = PieceSvgs[p]?.Draw(size, size);
            }
            pieceImageSize = size;
        }

        private void DrawPiece(Graphics g, Piece p, Rectangle r)
        {
            Bitmap? image = pieceImages[(int)p];
            if (image != null) g.DrawImage(image, r);
        }

        // Red radial glow under a king in check, as on lichess.
        private static void DrawCheckGlow(Graphics g, Rectangle r)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(r.X - r.Width / 4, r.Y - r.Height / 4, r.Width * 3 / 2, r.Height * 3 / 2);
            using var glow = new PathGradientBrush(path)
            {
                CenterColor = CheckTint,
                SurroundColors = new[] { Color.FromArgb(0, 255, 0, 0) },
            };
            var clip = g.Clip;
            g.SetClip(r);
            g.FillPath(glow, path);
            g.Clip = clip;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer.Dispose();
                foreach (Bitmap? image in pieceImages) image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
