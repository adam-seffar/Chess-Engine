using Chess_Engine_2DArray_C_;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using static Chess_Engine_2DArray_C_.Board;

class Program
{
    static void Main()
    {
        
    }

    static void TestFenPosition(Board board, string fen, string description)
    {
        Console.WriteLine($"\n{description}: {fen}");
        board.LoadFromFen(fen);
        board.PrintBoard();
    
        Console.WriteLine($"Active color: {(board.WhiteToMove ? "White" : "Black")}");
        Console.WriteLine($"Castling rights: {board.Castling}");
        Console.WriteLine($"En passant: {(board.EnPassantTargetFile.HasValue ? 
            $"{(char)('a' + board.EnPassantTargetFile.Value)}{(board.WhiteToMove ? "6" : "3")}" : "-")}");
        Console.WriteLine($"Halfmove clock: {board.HalfmoveClock}");
        Console.WriteLine($"Fullmove number: {board.FullmoveNumber}");
    }
       static void PerftTest(Board board, int depth)
       {
           MoveGenerator generator = new MoveGenerator();
           LegalMoveFilter filter = new LegalMoveFilter();

           Console.WriteLine($"Perft depth {depth}");
           int totalNodes = 0;

           var moves = generator.GenerateMoves(board, board.WhiteToMove);
           var legalMoves = filter.FilterLegalMoves(board, moves, board.WhiteToMove);

           // Early return if no legal moves
           if (legalMoves.Count == 0)
           {
               Console.WriteLine("No legal moves (checkmate/stalemate)");
               return;
           }

           foreach (var move in legalMoves)
           {
               // Save state
               var state = new MoveState
               {
                   CapturedPiece = board.squares[move.ToRank, move.ToFile],
                   EnPassantTargetFile = board.EnPassantTargetFile,
                   WhiteToMove = board.WhiteToMove,
                   CastlingRights = board.Castling.Clone(), // if Castling is a class, use Clone or deep copy
                   WasEnPassant = (board.squares[move.ToRank, move.ToFile] == Piece.Empty &&
                                   board.squares[move.FromRank, move.FromFile] is Piece.WP or Piece.BP &&
                                   move.ToFile != move.FromFile)
               };

               board.MakeMove(move, board.WhiteToMove);
               board.WhiteToMove = !board.WhiteToMove;

               nodes += Perft(board, depth - 1);

               board.WhiteToMove = state.WhiteToMove;
               board.EnPassantTargetFile = state.EnPassantTargetFile;
               board.Castling = state.CastlingRights;
               board.UnmakeMove(move, state.CapturedPiece, state.WasEnPassant, state.CastlingRights);
           }



           Console.WriteLine($"Total nodes: {totalNodes}");
       }

       static int Perft(Board board, int depth)
       {
           if (depth == 0) return 1;

           MoveGenerator generator = new MoveGenerator();
           LegalMoveFilter filter = new LegalMoveFilter();
           int nodes = 0;

           var moves = generator.GenerateMoves(board, board.WhiteToMove);
           var legalMoves = filter.FilterLegalMoves(board, moves, board.WhiteToMove);

           if (legalMoves.Count == 0) return 0;

           foreach (var move in legalMoves)
           {
               Piece captured = board.MakeMove(move, board.WhiteToMove);

               // Flip side to move
               board.WhiteToMove = !board.WhiteToMove;

               nodes += Perft(board, depth - 1);

               // Undo turn
               board.WhiteToMove = !board.WhiteToMove;

               board.UnmakeMove(move, captured, false, board.Castling);
           }

           return nodes;
       }

}
