using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        private bool me;
        private int nodes;
        private Random rng;
        private Timer timer;

        private const int lVal = 100000;

        private List<Move> workingEqualToBest;
        private Move best;
        private int bestScore;

        private Move bestFoundEver;
        private List<Move> equalToBest = new();

        private int maxTimeAllowed;

        public EvilBot()
        {
            Logger.Init();
            rng = new Random();
        }

        public Move Think(Board board, Timer timer)
        {
            if (board.PlyCount == 0) return new Move("e2e4", board);
            if (board.PlyCount == 1) return new Move("d7d5", board);

            Console.WriteLine($"EG Score: {CalculateEGScore(board)}");
            // foreach (var list in board.GetAllPieceLists())
            // {
            //     foreach (var piece in list)
            //     {
            //         var pst = PiecePosScore(piece);
            //         Console.WriteLine($"{piece.Square} {piece.PieceType} {pst}");
            //     }
            // }

            nodes = 0;
            me = board.IsWhiteToMove;
            this.timer = timer;

            Move[] moves = board.GetLegalMoves();

            FindBestIter(board, moves);

            if (equalToBest.Count > 0) bestFoundEver = equalToBest[rng.Next(equalToBest.Count)];

            if (bestFoundEver == Move.NullMove)
            {
                bestFoundEver = moves[rng.Next(moves.Length)];
                Console.WriteLine($"Was forced to use pure random move! Best found ever is null!");
            }


            Console.WriteLine($"Eval: {bestScore}. Number of node searched: {nodes}. Spent: {timer.MillisecondsElapsedThisTurn}ms");

            return bestFoundEver;
        }

        private void FindBestIter(Board board, Move[] moves)
        {
            int depth = 1;
            int bufferTime = 5000; // Save 5 seconds of time, this just helps prevent timeouts near endgames
            int tLeft = timer.MillisecondsRemaining - bufferTime;
            int div = 30;
            if (tLeft < 30000) div = 15;
            if (tLeft < 15000) div = 10;

            maxTimeAllowed = Math.Max(tLeft / div, 50);

            while (true)
            {
                bestScore = -100000;
                best = Move.NullMove;
                workingEqualToBest = new List<Move>();

                // If we have less than 50% of our time left, no point even trying another cycle. We won't have time for it
                float precTimeLeft = ((float)maxTimeAllowed - timer.MillisecondsElapsedThisTurn) / maxTimeAllowed;
                if (precTimeLeft < 0.5f)
                {
                    // Console.WriteLine($"PrecTimeLeft: {precTimeLeft}. Allowed: {maxTimeAllowed}, used: {timer.MillisecondsElapsedThisTurn}");
                    Console.WriteLine($"Ending at depth {depth} after {timer.MillisecondsElapsedThisTurn}ms");
                    if (bestFoundEver == Move.NullMove) bestFoundEver = best;
                    return;
                }

                foreach (var move in moves)
                {
                    if (timer.MillisecondsElapsedThisTurn > maxTimeAllowed)
                    {
                        Console.WriteLine($"Ending at depth {depth} after {timer.MillisecondsElapsedThisTurn}ms");
                        if (bestFoundEver == Move.NullMove) bestFoundEver = best;
                        return;
                    }

                    board.MakeMove(move);

                    if (move.IsEnPassant)
                    {
                        bestFoundEver = move;
                        equalToBest.Clear();
                        board.UndoMove(move);
                        Console.WriteLine("Holy hell.");
                        return;
                    }
                    var score = -MinimaxEval(board, depth);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = move;
                        workingEqualToBest.Clear();
                    }
                    else if (score == bestScore)
                    {
                        workingEqualToBest.Add(move);
                    }

                    board.UndoMove(move);
                }

                bestFoundEver = best;
                equalToBest = workingEqualToBest;

                depth++;
            }

        }

        // private void FindBestWithRetry(Board board, Move[] moves, int depthToUse)
        // {
        //     bestScore = -100000;
        //     best = Move.NullMove;
        //     workingEqualToBest = new List<Move>();
        // 
        //     foreach (var move in moves)
        //     {
        //         board.MakeMove(move);
        // 
        //         if (move.IsEnPassant)
        //         {
        //             best = move;
        //             workingEqualToBest.Clear();
        //             Console.WriteLine("Holy hell.");
        //             return;
        //         }
        // 
        //         var score = MinimaxEval(board, depthToUse); // If we are black, invert score. Black is winning when negative
        // 
        //         // We hit a retry! We need to go quicker
        //         if (needsRetry)
        //         {
        //             Console.WriteLine($"Starting retry!");
        //             needsRetry = false;
        //             board.UndoMove(move);
        //             FindBestWithRetry(board, moves, searchDepth - 2);
        //             return;
        //         }
        // 
        //         var resultScore = score * Mult(me);
        //         if (resultScore > bestScore)
        //         {
        //             bestScore = resultScore;
        //             best = move;
        //             workingEqualToBest.Clear();
        //         }
        //         else if (resultScore == bestScore)
        //         {
        //             workingEqualToBest.Add(move);
        //         }
        // 
        //         board.UndoMove(move);
        //     }
        // }

        private int MinimaxEval(Board board, int depth, int alpha = -lVal, int beta = lVal)
        {
            if (timer.MillisecondsElapsedThisTurn > maxTimeAllowed) return 0;

            nodes++;
            if (board.IsInCheckmate()) return -lVal;
            if (board.IsDraw()) return 0;

            if (depth == 0)
            {
                return CheckCaptures(board, alpha, beta);// Evaluate(board);
            }

            var moves = GetSortedMoves(board);
            foreach (var move in moves)
            {
                board.MakeMove(move);
                int moveScore = -MinimaxEval(board, depth - 1, -beta, -alpha);
                board.UndoMove(move);

                alpha = Math.Max(alpha, moveScore);
                if (alpha >= beta) break;
            }

            return alpha;
        }

        private int CheckCaptures(Board board, int alpha, int beta)
        {
            int pat = Evaluate(board);
            if (pat >= beta) return beta;
            if (alpha < pat) alpha = pat;

            var captures = board.GetLegalMoves(true);
            foreach (var capture in captures)
            {
                board.MakeMove(capture);
                int score = -CheckCaptures(board, -beta, -alpha);
                board.UndoMove(capture);

                if (score >= beta) return beta;
                if (score > alpha) alpha = score;
            }

            return alpha;
        }

        private Move[] GetSortedMoves(Board board)
        {
            var moves = board.GetLegalMoves();
            List<List<Move>> sMoves = new() { new(), new(), new() };
            foreach (var move in moves)
            {
                if (move.IsCapture) sMoves[0].Add(move);
                else if (board.SquareIsAttackedByOpponent(move.TargetSquare)) sMoves[2].Add(move);
                else sMoves[1].Add(move);
            }

            return sMoves[0].Concat(sMoves[1]).Concat(sMoves[2]).ToArray();
        }

        private int BoolToMult(bool color)
        {
            return color ? 1 : -1;
        }

        public int Evaluate(Board board)
        {
            var mobilityMult = 1;
            var eval = CalculateMaterial(board) * BoolToMult(board.IsWhiteToMove);
            if (board.IsInCheck()) eval -= 50; // Check is bad thing for the person who's current move it is
            var mobility = CalculateMobility(board);
            eval += mobility * mobilityMult;
            return eval;
        }

        private int CalculateMobility(Board board)
        {
            var moves = board.GetLegalMoves();
            return moves.Length;
        }

        private int PieceWorth(PieceType type)
        {
            int[] scores = { 0, 100, 300, 350, 500, 900, 10000 };
            return scores[(int)type];
        }

        private enum ScoreType { Pawn, Knight, Bishop, Rook, Queen, King, KingEndgame, KingHunt };

        //Assuming you put your packed data table into a table called packedScores.
        private int GetPieceBonusScore(ScoreType type, bool isWhite, int rank, int file)
        {
            ulong[,] kPackedScores =
       {
        {0x31CDE1EBFFEBCE00, 0x31D7D7F5FFF5D800, 0x31E1D7F5FFF5E200, 0x31EBCDFAFFF5E200},
        {0x31E1E1F604F5D80A, 0x13EBD80009FFEC0A, 0x13F5D8000A000014, 0x13FFCE000A00001E},
        {0x31E1E1F5FAF5E232, 0x13F5D80000000032, 0x0013D80500050A32, 0x001DCE05000A0F32},
        {0x31E1E1FAFAF5E205, 0x13F5D80000050505, 0x001DD80500050F0A, 0xEC27CE05000A1419},
        {0x31E1EBFFFAF5E200, 0x13F5E20000000000, 0x001DE205000A0F00, 0xEC27D805000A1414},
        {0x31E1F5F5FAF5E205, 0x13F5EC05000A04FB, 0x0013EC05000A09F6, 0x001DEC05000A0F00},
        {0x31E213F5FAF5D805, 0x13E214000004EC0A, 0x140000050000000A, 0x14000000000004EC},
        {0x31CE13EBFFEBCE00, 0x31E21DF5FFF5D800, 0x31E209F5FFF5E200, 0x31E1FFFB04F5E200},
    };

            //Because the arrays are 8x4, we need to mirror across the files.
            if (file > 3) file = 7 - file;
            //Additionally, if we're checking black pieces, we need to flip the board vertically.
            if (!isWhite) rank = 7 - rank;
            int unpackedData = 0;
            ulong bytemask = 0xFF;
            //first we shift the mask to select the correct byte              ↓
            //We then bitwise-and it with PackedScores            ↓
            //We finally have to "un-shift" the resulting data to properly convert back       ↓
            //We convert the result to an sbyte, then to an int, to ensure it converts properly.
            unpackedData = (int)(sbyte)((kPackedScores[rank, file] & (bytemask << (int)type)) >> (int)type);
            //inverting eval scores for black pieces
            if (!isWhite) unpackedData *= -1;
            return unpackedData;
        }


        private int PiecePosScore(Piece piece)
        {
            var st = (ScoreType)((int)piece.PieceType - 1);
            return GetPieceBonusScore(st, piece.IsWhite, piece.Square.Rank, piece.Square.File) / 10;
        }

        private int CalculateEGScore(Board board)
        {
            var lists = board.GetAllPieceLists();
            int[] endGameMults = { 0, 0, 2, 2, 1, 3, 0 };
            int egScore = 0;

            for (int i = 0; i < lists.Length; i++)
            {
                var list = lists[i];
                egScore += endGameMults[(int)list.TypeOfPieceInList] * list.Count;
            }

            return egScore;
        }

        public int CalculateMaterial(Board board)
        {
            int eval = 0;
            var lists = board.GetAllPieceLists();
            int[] endGameMults = { 0, 0, 2, 2, 3, 4, 0 };
            int egScore = 0;

            foreach (var list in lists)
            {
                eval += PieceWorth(list.TypeOfPieceInList) * list.Count * BoolToMult(list.IsWhitePieceList);
                // foreach (var piece in list) eval += PiecePosScore(piece);
                egScore += endGameMults[(int)list.TypeOfPieceInList] * list.Count;
            }

            return eval;
        }
    }
}