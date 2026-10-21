using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Engine_2DArray_C_
{
    internal class Game
    {
        private Board board;
        private MoveGenerator generator;
        private LegalMoveFilter filter;
        private bool whiteToMove;
        private List<string> boardHistory;  

        public Game()
        {
            board = new Board();
            generator = new MoveGenerator();
            filter = new LegalMoveFilter();
            whiteToMove = true;
            boardHistory = new List<string>();

            board.LoadFromFen(board.startingFenPos);
            boardHistory.Add(board.startingFenPos);
        }

        // After each move, call this function to track the FEN
        public void AddToHistory()
        {
            boardHistory.Add(board.ToFen());  // Convert the board to a FEN string and add it
        }
    }
}
