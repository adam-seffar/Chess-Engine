using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Engine_2DArray_C_
{
    public enum Piece
    {
        Empty,
        WP, WN, WB, WR, WQ, WK,
        BP, BN, BB, BR, BQ, BK
    }

    public class Board
    {

        public Piece[,] squares = new Piece[8, 8];
        public string startingFenPos = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1\r\n";
        public int? EnPassantTargetFile { get; set; }
        public CastlingRights Castling;
        public int HalfmoveClock { get; private set; } = 0;
        public int FullmoveNumber { get; private set; } = 1;
        public bool WhiteToMove { get; set; } = true;

        public Board() {
            Castling = new CastlingRights
            {
                WhiteKingside = true,
                WhiteQueenside = true,
                BlackKingside = true,
                BlackQueenside = true
            };
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    squares[i, j] = Piece.Empty;
                }
            }
        }
        public struct CastlingRights
        {
            public bool WhiteKingside;
            public bool WhiteQueenside;
            public bool BlackKingside;
            public bool BlackQueenside;

            public override string ToString()
            {
                if (!WhiteKingside && !WhiteQueenside && !BlackKingside && !BlackQueenside)
                    return "-";

                return $"{(WhiteKingside ? "K" : "")}{(WhiteQueenside ? "Q" : "")}" +
                       $"{(BlackKingside ? "k" : "")}{(BlackQueenside ? "q" : "")}";
            }
        }

        //COMPLETE FEN PARSE
        public void LoadFromFen(string fen)
        {
            string[] fenParts = fen.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (fenParts.Length < 4)
                throw new ArgumentException("Invalid FEN string - too few parts");

            // 1. Piece placement
            ParsePiecePlacement(fenParts[0]);

            // 2. Active color
            WhiteToMove = fenParts[1] == "w";

            // 3. Castling availability
            ParseCastlingRights(fenParts[2]);

            // 4. En passant
            ParseEnPassant(fenParts[3]);

            // 5. Halfmove clock (optional)
            if (fenParts.Length > 4)
                HalfmoveClock = int.Parse(fenParts[4]);

            // 6. Fullmove number (optional)
            if (fenParts.Length > 5)
                FullmoveNumber = int.Parse(fenParts[5]);
        }

        private void ParsePiecePlacement(string placement)
        {
            // Clear the board
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                    squares[i, j] = Piece.Empty;

            string[] ranks = placement.Split('/');
            if (ranks.Length != 8)
                throw new ArgumentException("Invalid FEN - must have 8 ranks");

            for (int rank = 0; rank < 8; rank++)
            {
                int file = 0;
                foreach (char c in ranks[rank])
                {
                    if (char.IsDigit(c))
                    {
                        file += c - '0'; // Skip empty squares
                    }
                    else
                    {
                        squares[rank, file] = FenCharToPiece(c);
                        file++;
                    }
                }
                if (file != 8)
                    throw new ArgumentException($"Invalid FEN - rank {rank + 1} has {file} files");
            }
        }

        private void ParseCastlingRights(string castling)
        {
            // Reset all castling rights
            Castling = new CastlingRights
            {
                WhiteKingside = false,
                WhiteQueenside = false,
                BlackKingside = false,
                BlackQueenside = false
            };

            if (castling == "-") return;

            foreach (char c in castling)
            {
                switch (c)
                {
                    case 'K': Castling.WhiteKingside = true; break;
                    case 'Q': Castling.WhiteQueenside = true; break;
                    case 'k': Castling.BlackKingside = true; break;
                    case 'q': Castling.BlackQueenside = true; break;
                    default:
                        throw new ArgumentException($"Invalid castling character: {c}");
                }
            }
        }

        private void ParseEnPassant(string enPassant)
        {
            if (enPassant == "-")
            {
                EnPassantTargetFile = null;
                return;
            }

            if (enPassant.Length != 2)
                throw new ArgumentException("Invalid en passant square");

            int file = char.ToLower(enPassant[0]) - 'a';
            int rank = 8 - (enPassant[1] - '0');

            if (file < 0 || file > 7 || rank < 0 || rank > 7)
                throw new ArgumentException("Invalid en passant square coordinates");

            // En passant target is always on rank 3 or 4
            if (rank != 2 && rank != 5)
                throw new ArgumentException("Invalid en passant rank");

            EnPassantTargetFile = file;
        }

        public string ToFen()
        {
            // 1. Piece Placement
            string piecePlacement = "";
            for (int rank = 7; rank >= 0; rank--)  // Go from rank 8 to rank 1
            {
                int emptyCount = 0;
                for (int file = 0; file < 8; file++)  // Traverse files from a to h
                {
                    Piece piece = squares[rank, file];
                    if (piece == Piece.Empty)
                    {
                        emptyCount++;
                    }
                    else
                    {
                        if (emptyCount > 0)
                        {
                            piecePlacement += emptyCount.ToString();
                            emptyCount = 0;
                        }

                        piecePlacement += PieceToChar(piece);
                    }
                }

                if (emptyCount > 0)
                {
                    piecePlacement += emptyCount.ToString();
                }

                if (rank > 0)
                {
                    piecePlacement += "/";  // Separate ranks with a slash
                }
            }

            // 2. Active Color
            string activeColor = WhiteToMove ? "w" : "b";

            // 3. Castling Rights
            string castlingRights = Castling.ToString();
            if (castlingRights == "-")
            {
                castlingRights = "";
            }

            // 4. En Passant Target
            string enPassant = EnPassantTargetFile.HasValue ?
                $"{(char)('a' + EnPassantTargetFile.Value)}{(WhiteToMove ? "6" : "3")}" : "-";

            // 5. Halfmove Clock
            string halfmoveClock = HalfmoveClock.ToString();

            // 6. Fullmove Number
            string fullmoveNumber = FullmoveNumber.ToString();

            // Combine all parts into the final FEN string
            return $"{piecePlacement} {activeColor} {castlingRights} {enPassant} {halfmoveClock} {fullmoveNumber}";
        }


        //PRINT

        public void PrintBoard()
        {
            for (int rank = 0; rank < 8; rank++)
            {
                Console.Write(8 - rank + " ");  // Print rank number

                for (int file = 0; file < 8; file++)
                {
                    Console.Write(PieceToChar(squares[rank, file]) + " ");
                }

                Console.WriteLine();
            }

            Console.WriteLine("  a b c d e f g h");  // Print file letters
        }



        //MAKE AND UNMAKEMOVE
        public Piece MakeMove(Move move, bool whiteToMove)
        {
            Piece movingPiece = squares[move.FromRank, move.FromFile];
            Piece capturedPiece = squares[move.ToRank, move.ToFile];

            // Save castling state for unmake
            CastlingRights originalCastling = Castling;

            // Update castling rights if king or rook moves
            if (movingPiece == Piece.WK)
            {
                Castling.WhiteKingside = false;
                Castling.WhiteQueenside = false;
            }
            else if (movingPiece == Piece.BK)
            {
                Castling.BlackKingside = false;
                Castling.BlackQueenside = false;
            }
            else if (movingPiece == Piece.WR)
            {
                if (move.FromFile == 0 && move.FromRank == 7) Castling.WhiteQueenside = false;
                if (move.FromFile == 7 && move.FromRank == 7) Castling.WhiteKingside = false;
            }
            else if (movingPiece == Piece.BR)
            {
                if (move.FromFile == 0 && move.FromRank == 0) Castling.BlackQueenside = false;
                if (move.FromFile == 7 && move.FromRank == 0) Castling.BlackKingside = false;
            }

            // Handle castling: move rook
            if ((movingPiece == Piece.WK || movingPiece == Piece.BK) &&
                Math.Abs(move.ToFile - move.FromFile) == 2)
            {
                int rookFromFile, rookToFile;
                int rank = move.FromRank;  // King's rank

                if (move.ToFile > move.FromFile) // Kingside
                {
                    rookFromFile = 7;
                    rookToFile = move.ToFile - 1;
                }
                else // Queenside
                {
                    rookFromFile = 0;
                    rookToFile = move.ToFile + 1;
                }

                // Move rook
                squares[rank, rookToFile] = squares[rank, rookFromFile];
                squares[rank, rookFromFile] = Piece.Empty;
            }

            // Move king
            squares[move.ToRank, move.ToFile] = movingPiece;
            squares[move.FromRank, move.FromFile] = Piece.Empty;

            // Handle castling move
            if (movingPiece == Piece.WK || movingPiece == Piece.BK)
            {
                int castlingRow = whiteToMove ? 7 : 0;

                // Kingside castling
                if (move.ToFile - move.FromFile == 2)
                {
                    squares[castlingRow, 5] = squares[castlingRow, 7];
                    squares[castlingRow, 7] = Piece.Empty;
                }
                // Queenside castling
                else if (move.FromFile - move.ToFile == 2)
                {
                    squares[castlingRow, 3] = squares[castlingRow, 0];
                    squares[castlingRow, 0] = Piece.Empty;
                }
            }


            // Clear previous en passant target
            EnPassantTargetFile = null;

            // Handle en passant capture
            if ((movingPiece == Piece.WP || movingPiece == Piece.BP) &&
                move.ToFile != move.FromFile && capturedPiece == Piece.Empty)
            {
                capturedPiece = squares[move.FromRank, move.ToFile];
                squares[move.FromRank, move.ToFile] = Piece.Empty;
            }

            squares[move.ToRank, move.ToFile] = movingPiece;
            squares[move.FromRank, move.FromFile] = Piece.Empty;

            //Sets the en passant target
            if ((movingPiece == Piece.WP && move.FromRank == 6 && move.ToRank == 4) ||
                (movingPiece == Piece.BP && move.FromRank == 1 && move.ToRank == 3))
            {
                EnPassantTargetFile = move.FromFile;
            }
            if (movingPiece == Piece.WP || movingPiece == Piece.BP || capturedPiece != Piece.Empty)
                HalfmoveClock = 0;
            else
                HalfmoveClock++;

            return capturedPiece;
        }

        public void UnmakeMove(Move move, Piece capturedPiece, bool wasEnPassant, CastlingRights originalCastling)
        {
            Piece movingPiece = squares[move.ToRank, move.ToFile];

            // Restore castling rights first (before any piece movement)
            Castling = originalCastling;

            // Handle castling: move rook back
            if ((movingPiece == Piece.WK || movingPiece == Piece.BK) &&
                Math.Abs(move.ToFile - move.FromFile) == 2)
            {
                int rookFromFile, rookToFile;
                int rank = move.FromRank;  // King's rank

                if (move.ToFile > move.FromFile) // Kingside
                {
                    rookToFile = 7;
                    rookFromFile = move.ToFile - 1;
                }
                else // Queenside
                {
                    rookToFile = 0;
                    rookFromFile = move.ToFile + 1;
                }

                // Move rook back
                squares[rank, rookToFile] = squares[rank, rookFromFile];
                squares[rank, rookFromFile] = Piece.Empty;
            }

            // Move king/piece back to original position
            squares[move.FromRank, move.FromFile] = movingPiece;
            squares[move.ToRank, move.ToFile] = Piece.Empty;

            // Restore captured piece (with en passant handling)
            if (wasEnPassant)
            {
                // For en passant, the captured pawn was on the same rank
                int capturedRank = movingPiece == Piece.WP ? move.ToRank + 1 : move.ToRank - 1;
                squares[move.FromRank, move.ToFile] = capturedPiece; // Original en passant capture square
            }
            else if (capturedPiece != Piece.Empty)
            {
                squares[move.ToRank, move.ToFile] = capturedPiece;
            }
        }


        // CHECK, CHECKMATE AND DRAW LOGIC
        public bool IsInCheck(bool whiteKing)
        {
            Piece king = whiteKing ? Piece.WK : Piece.BK;
            (int kingRank, int kingFile) = FindKing(whiteKing);

            if (kingRank == -1 || kingFile == -1)
                throw new Exception("King not found!");

            return IsSquareAttacked(kingRank, kingFile, !whiteKing);
        }

        public bool IsCheckmate(bool whiteToMove)
        {
            MoveGenerator generator = new MoveGenerator();
            LegalMoveFilter filter = new LegalMoveFilter();

            var pseudoMoves = generator.GenerateMoves(this, whiteToMove);
            var legalMoves = filter.FilterLegalMoves(this, pseudoMoves, whiteToMove);

            return legalMoves.Count == 0 && IsInCheck(whiteToMove);
        }

        private bool IsDrawCondition(bool whiteToMove)
        {
            return IsStaleMateBy50MoveRule(whiteToMove)
                || IsStaleMateByNoLegalMove(whiteToMove)
                || IsStaleMateByInsufficientMaterial()
                || IsStaleMateByThreeFoldRepetition(whiteToMove);
        }

        private bool IsStaleMateByNoLegalMove(bool whiteToMove)
        {
            MoveGenerator generator = new MoveGenerator();
            LegalMoveFilter filter = new LegalMoveFilter();

            var pseudoMoves = generator.GenerateMoves(this, whiteToMove);
            var legalMoves = filter.FilterLegalMoves(this, pseudoMoves, whiteToMove);
            return (!IsInCheck(whiteToMove) && legalMoves.Count == 0) ;
        }

        private bool IsStaleMateByInsufficientMaterial()
        {
            int whitePieces = 0, blackPieces = 0;
            bool whiteHasNonPawn = false, blackHasNonPawn = false;

            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    Piece piece = squares[rank, file];
                    if (piece == Piece.Empty) continue;

                    if (IsWhite(piece))
                    {
                        if(piece == Piece.WQ || piece == Piece.WR)
                        {
                            return false;
                        }
                        whitePieces++;
                        if (piece != Piece.WP && piece != Piece.WK)
                            whiteHasNonPawn = true;
                    }
                    else
                    {
                        if (piece == Piece.BQ || piece == Piece.BR)
                        {
                            return false;
                        }
                        blackPieces++;
                        if (piece != Piece.BP && piece != Piece.BK)
                            blackHasNonPawn = true;
                    }
                }
            }

            // King vs. King
            if (whitePieces == 1 && blackPieces == 1)
                return true;

            // King + Bishop/Knight vs. King
            if ((whitePieces == 2 && !whiteHasNonPawn) || (blackPieces == 2 && !blackHasNonPawn))
                return true;

            return false;
        }

        private bool IsStaleMateBy50MoveRule(bool whiteToMove)
        {
            return HalfmoveClock >= 50;
        }

        private bool IsStaleMateByThreeFoldRepetition(bool whiteToMove)
        {
            string currentFen = board.ToFen();
            int count = boardHistory.Count(fen => fen == currentFen);
            return count >= 3;
        }

        //HELPER FUNCTIONS
        private bool IsWhite(Piece piece)
        {
            return piece >= Piece.WP && piece <= Piece.WK;
        }
        public (int rank, int file)? EnPassantTargetSquare
        {
            get
            {
                if (!EnPassantTargetFile.HasValue) return null;
                // En passant target is always on rank 3 or 4 (for white or black perspective)
                int rank = EnPassantTargetFile.HasValue ?
                    (squares[3, EnPassantTargetFile.Value] == Piece.BP ? 2 : 5) : -1;
                return (rank, EnPassantTargetFile.Value);
            }
        }


        private (int rank, int file) FindKing(bool whiteKing)
        {
            Piece king = whiteKing ? Piece.WK : Piece.BK;
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    if (squares[rank, file] == king)
                        return (rank, file);
                }
            }
            return (-1, -1); // King not found (should never happen in a valid game)
        }

        private bool IsSquareAttacked(int targetRank, int targetFile, bool byWhite)
        {
            // Reuse the existing `IsSquareAttacked` from `MoveGenerator.cs`
            MoveGenerator generator = new MoveGenerator();
            return generator.IsSquareAttacked(this, targetRank, targetFile, byWhite);
        }

        public Piece SquareContents(int file, int rank)
        {
            return squares[rank, file];
        }

        private char PieceToChar(Piece p)
        {
            return p switch
            {
                Piece.WP => 'P',
                Piece.WN => 'N',
                Piece.WB => 'B',
                Piece.WR => 'R',
                Piece.WQ => 'Q',
                Piece.WK => 'K',
                Piece.BP => 'p',
                Piece.BN => 'n',
                Piece.BB => 'b',
                Piece.BR => 'r',
                Piece.BQ => 'q',
                Piece.BK => 'k',
                _ => '.'
            };
        }

        private Piece FenCharToPiece(char c)
        {
            return c switch
            {
                'P' => Piece.WP,
                'N' => Piece.WN,
                'B' => Piece.WB,
                'R' => Piece.WR,
                'Q' => Piece.WQ,
                'K' => Piece.WK,
                'p' => Piece.BP,
                'n' => Piece.BN,
                'b' => Piece.BB,
                'r' => Piece.BR,
                'q' => Piece.BQ,
                'k' => Piece.BK,
                _ => Piece.Empty
            };
        }

    }
}
// Original MakeMove & UnmakeMove
//public Piece MakeMove(Move move)
//{

//    Piece movingPiece = squares[move.FromRank, move.FromFile];
//    Piece capturedPiece = squares[move.ToRank, move.ToFile];

//    squares[move.ToRank, move.ToFile] = movingPiece;
//    squares[move.FromRank, move.FromFile] = Piece.Empty;

//    return capturedPiece;
//}
//public void UnmakeMove(Move move, Piece capturedPiece)
//{
//    Piece movingPiece = squares[move.ToRank, move.ToFile];

//    squares[move.FromRank, move.FromFile] = movingPiece;
//    squares[move.ToRank, move.ToFile] = capturedPiece;
//}