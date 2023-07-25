using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    private bool me;
    private int checks = 0;
    private Random rng;
    private const int defaultSearchDepth = 5;
    private float lastTimeSpent = 0;
    private int searchDepth = 3;
    private Timer timer;
    private int maxTime = 5000;
    private bool needsRetry = false;
    private bool isOnRetry = false;
    private List<Move> equalToBest;
    private Move best;
    private int bestScore;

    public MyBot()
    {
        Logger.Init();
        rng = new Random();
    }

    public Move Think(Board board, Timer timer)
    {
        checks = 0;
        me = board.IsWhiteToMove;
        this.timer = timer;
        needsRetry = false;
        isOnRetry = false;

        Move[] moves = board.GetLegalMoves();

        UpdateDepthSetting();
        FindBestWithRetry(board, moves, searchDepth);

        if (equalToBest.Count > 0)
        {
            best = equalToBest[rng.Next(equalToBest.Count)];
        }

        if (best == Move.NullMove) best = moves[rng.Next(moves.Length)];


        lastTimeSpent = timer.MillisecondsElapsedThisTurn;
        Console.WriteLine($"Eval: {bestScore}. Number of node searched: {checks}. Used depth: {searchDepth}. Spent: {lastTimeSpent}ms");
        return best;
    }

    private void UpdateDepthSetting()
    {
        int maxTimePerMove = 2000;
        int minTimePerMove = 500;

        var left = timer.MillisecondsRemaining;
        if (left < 30000) { maxTimePerMove = 1000; minTimePerMove = 250; }
        if (left < 15000) { maxTimePerMove = 500; minTimePerMove = 150; }
        if (left < 5000) { maxTimePerMove = 500; minTimePerMove = 50; }

        if (lastTimeSpent > maxTimePerMove) searchDepth--;
        if (lastTimeSpent < minTimePerMove) searchDepth++;
    }

    private void FindBestWithRetry(Board board, Move[] moves, int depthToUse)
    {
        bestScore = -100000;
        best = Move.NullMove;
        equalToBest = new List<Move>();

        foreach (var move in moves)
        {
            board.MakeMove(move);

            if (move.IsEnPassant)
            {
                best = move;
                equalToBest.Clear();
                Console.WriteLine("Holy hell.");
                return;
            }

            var score = MinimaxEval(board, depthToUse); // If we are black, invert score. Black is winning when negative

            // We hit a retry! We need to go quicker
            if (needsRetry)
            {
                Console.WriteLine($"Starting retry!");
                needsRetry = false;
                board.UndoMove(move);
                FindBestWithRetry(board, moves, searchDepth - 2);
                return;
            }

            var resultScore = score * Mult(me);
            if (resultScore > bestScore)
            {
                bestScore = resultScore;
                best = move;
                equalToBest.Clear();
            }
            else if (resultScore == bestScore)
            {
                equalToBest.Add(move);
            }

            board.UndoMove(move);
        }
    }

    private int MinimaxEval(Board board, int depth = defaultSearchDepth, int alpha = int.MinValue, int beta = int.MaxValue)
    {
        if (!isOnRetry && timer.MillisecondsElapsedThisTurn > maxTime)
        {
            needsRetry = true;
            isOnRetry = true;
            Console.WriteLine($"Time limit expired!");
        }
        if (needsRetry) return 0;

        checks++;
        if (board.IsInCheckmate()) return -100000 * Mult(board.IsWhiteToMove); // Checkmate and white to move = black won
        if (board.IsDraw()) return 0;

        if (depth == 0)
        {
            return Evaluate(board);
        }

        var moves = GetSortedMoves(board);
        foreach (var move in moves)
        {
            board.MakeMove(move);
            int moveScore = MinimaxEval(board, depth - 1, alpha, beta);
            board.UndoMove(move);

            if (board.IsWhiteToMove) alpha = Math.Max(alpha, moveScore);
            else beta = Math.Min(beta, moveScore);

            if (beta <= alpha) break;
        }

        if (board.IsWhiteToMove) return alpha; // White wants highest number
        else return beta; // Black wants least
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

    private int Mult(bool color)
    {
        return color ? 1 : -1;
    }

    public int Evaluate(Board board)
    {
        var eval = CalculateMaterial(board);
        if (board.IsInCheck()) eval -= 100 * Mult(board.IsWhiteToMove); // White to move && check = white is in check, so subtract from eval

        return eval;
    }

    // public int CalculateBoardCoverage(Board board)
    // {
    //     // board.GetKingSquare
    //     for (int i = 0; i < 64; i++)
    //     {
    //         var square = new Square(i);
    //         // board.SquareIsAttackedByOpponent(square);
    //         
    //     }
    // }

    private int PieceWorth(PieceType type)
    {
        int[] scores = { 0, 100, 300, 350, 500, 900, 10000 };
        return scores[(int)type];
    }

    public int CalculateMaterial(Board board)
    {
        int eval = 0;
        foreach (var list in board.GetAllPieceLists())
        {
            eval += PieceWorth(list.TypeOfPieceInList) * list.Count * (list.IsWhitePieceList == true ? 1 : -1);
        }

        return eval;
    }
}