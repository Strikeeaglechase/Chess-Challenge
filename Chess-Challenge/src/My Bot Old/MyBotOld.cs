using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

public class MyBotOld : IChessBot
{
    private bool me;
    private int nodes;
    private int qNodes;
    private Random rng;
    private Timer timer;

    private const int lVal = 100000;

    private List<Move> currentEqualToBest;
    private Move currentItrBest;
    private int currentBestScore;

    private Move bestFoundEver;
    private int bestFoundEverScore;
    private List<Move> equalToBest = new();

    private int maxTimeAllowed;
    public int curretItrDepth;


    public MyBotOld()
    {
        Logger.Init();
        rng = new Random(1776);
    }

    public Move Think(Board board, Timer timer)
    {
        if (board.PlyCount == 0) return new Move("e2e4", board);
        if (board.PlyCount == 1) return new Move("d7d5", board);

        // Console.WriteLine($"EG Score: {CalculateEGScore(board)}");
        // foreach (var list in board.GetAllPieceLists())
        // {
        //     foreach (var piece in list)
        //     {
        //         var pst = PiecePosScore(piece);
        //         Console.WriteLine($"{piece.Square} {piece.PieceType} {pst}");
        //     }
        // }

        nodes = 0;
        qNodes = 0;
        me = board.IsWhiteToMove;
        this.timer = timer;

        Move[] moves = board.GetLegalMoves();


        FindBestIter(board);

        if (equalToBest.Count > 0) bestFoundEver = equalToBest[rng.Next(equalToBest.Count)];

        if (bestFoundEver == Move.NullMove)
        {
            bestFoundEver = moves[rng.Next(moves.Length)];
            Console.WriteLine($"Was forced to use pure random move! Best found ever is null!");
        }


        Console.WriteLine($"Eval: {bestFoundEverScore}. Number of node searched: {nodes}. QNodes: {qNodes} Spent: {timer.MillisecondsElapsedThisTurn}ms");

        return bestFoundEver;
    }

    private void FindBestIter(Board board)
    {
        curretItrDepth = 1;
        int bufferTime = 5000; // Save 5 seconds of time, this just helps prevent timeouts near endgames
        int tLeft = timer.MillisecondsRemaining - bufferTime;
        int div = 30;
        if (tLeft < 30000) div = 15;
        if (tLeft < 15000) div = 10;

        maxTimeAllowed = Math.Max(tLeft / div, 50);
        bestFoundEver = Move.NullMove;
        equalToBest.Clear();
        bestFoundEverScore = -20000;

        while (true)
        {
            currentBestScore = -200000;
            currentItrBest = Move.NullMove;
            currentEqualToBest = new List<Move>();

            // If we have less than 50% of our time left, no point even trying another cycle. We won't have time for it
            float precTimeLeft = ((float)maxTimeAllowed - timer.MillisecondsElapsedThisTurn) / maxTimeAllowed;
            if (precTimeLeft < 0.5f)
            {
                Console.WriteLine($"Ending at depth {curretItrDepth} after {timer.MillisecondsElapsedThisTurn}ms due to <50% time left");
                if (bestFoundEver == Move.NullMove) bestFoundEver = currentItrBest;
                return;
            }

            // var moves = GetSortedMoves(board, bestFoundEver);
            var moves = GetSortedMoves(board, Move.NullMove);
            foreach (var move in moves)
            {
                // Console.WriteLine("TL Cycle");
                if (timer.MillisecondsElapsedThisTurn > maxTimeAllowed)
                {
                    Console.WriteLine($"Ending at depth {curretItrDepth} after {timer.MillisecondsElapsedThisTurn}ms");
                    // if (currentBestScore > bestFoundEverScore)
                    // {
                    //     Console.WriteLine($"Current itteration best is better despite being incomplete. Going with that!");
                    //     bestFoundEver = currentItrBest;
                    //     bestFoundEverScore = currentBestScore;
                    //     equalToBest = currentEqualToBest;
                    // }
                    if (bestFoundEver == Move.NullMove) bestFoundEver = currentItrBest;
                    return;
                }

                board.MakeMove(move);

                var score = -MinimaxEval(board, curretItrDepth);
                if (score > currentBestScore)
                {
                    currentBestScore = score;
                    currentItrBest = move;
                    currentEqualToBest.Clear();
                }
                else if (score == currentBestScore)
                {
                    currentEqualToBest.Add(move);
                }

                board.UndoMove(move);
            }

            bestFoundEver = currentItrBest;
            bestFoundEverScore = currentBestScore;
            equalToBest = currentEqualToBest;

            curretItrDepth++;
        }

    }

    private int MinimaxEval(Board board, int depth, int alpha = -lVal, int beta = lVal)
    {
        if (timer.MillisecondsElapsedThisTurn > maxTimeAllowed) return 0;
        nodes++;
        if (board.IsInCheckmate()) return -lVal + curretItrDepth;
        if (board.IsDraw()) return 0;

        if (depth == 0)
        {
            return CheckCaptures(board, alpha, beta);// Evaluate(board);
        }

        var moves = GetSortedMoves(board, Move.NullMove);
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
        qNodes++;
        int pat = Evaluate(board);
        if (pat >= beta) return beta;

        if (alpha < pat) alpha = pat;

        var captures = board.GetLegalMoves(true);
        // captures.
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

    // Quick sorting system that works without full array sorts
    private Move[] GetSortedMoves(Board board, Move forcedFirstMove)
    {
        List<List<Move>> sMoves = new() { new(), new(), new() };
        var moves = board.GetLegalMoves();
        if (forcedFirstMove != Move.NullMove) sMoves[0].Add(forcedFirstMove);

        foreach (var move in moves)
        {
            if (move.IsCapture || move.IsPromotion || move.IsCastles) sMoves[0].Add(move);
            else if (board.SquareIsAttackedByOpponent(move.TargetSquare)) sMoves[2].Add(move);
            else sMoves[1].Add(move);
        }

        return sMoves.SelectMany(x => x).ToArray();

    }

    // private Move[] GetSortedMoves(Board board, Move forcedFirstMove)
    // {
    //     var moves = board.GetLegalMoves();
    //     List<Tuple<int, Move>> sMoves = new() { };
    //     foreach (var move in moves)
    //     {
    //         int pVal = 0;
    //         if (move.IsCapture) pVal += PieceWorth(move.CapturePieceType);
    //         if (move.IsPromotion) pVal += PieceWorth(move.PromotionPieceType);
    //         if (move.IsCastles) pVal += 100;
    //         if (board.SquareIsAttackedByOpponent(move.TargetSquare)) pVal -= 5;
    // 
    //         sMoves.Add(new(pVal, move));
    //     }
    //     sMoves.Sort((a, b) => b.Item1.CompareTo(a.Item1));
    //     var arr = sMoves.Select(x => x.Item2);
    // 
    //     if (forcedFirstMove != Move.NullMove) return arr.Prepend(forcedFirstMove).ToArray();
    //     return arr.ToArray();
    // }

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
        int[] scores = { 0, 100, 320, 330, 500, 900, 10000 };
        return scores[(int)type];
    }

    // private int PiecePosScore(Piece piece)
    // {
    //     if (piece.PieceType == PieceType.Pawn) return (piece.IsWhite ? piece.Square.Rank : 7 - piece.Square.Rank) * 2;
    //     if (piece.PieceType == PieceType.King) return 0;
    //     // Distance from center
    //     var rankDist = Math.Abs(piece.Square.Rank - 4);
    //     var fileDist = Math.Abs(piece.Square.File - 4);
    // 
    //     return 5 - (rankDist + fileDist);
    // }

    static int PiecePosScore(Piece piece, bool kingEnd = false)
    {
        ulong[] values = { 231520005970560296, 371413138147804744, 376235690637563454, 587905079315048590, 448258289281955402, 876384074027600299, 448259453218158153, 948441732506814890, 452761880320570952, 952945332135234216, 457549218373334601, 889894855731940006, 340172791585601081, 614891522810808458, 344109996662162728, 326394734067484232 };
        int file = piece.Square.File;
        int rank = piece.Square.Rank;
        if (file > 3) file = 7 - file;
        if (piece.IsWhite) rank = 7 - rank;

        var index = file + rank * 4;
        var offset = ((int)piece.PieceType - 1) * 4 + (index % 2 * 32) + (kingEnd && piece.PieceType == PieceType.King ? 4 : 0);
        // Console.WriteLine($"Rank: {rank}, File: {file}, Index: {index}   Type Offset: {((int)type - 1) * 4}  Index Sub Offset: {(index % 2 * 32)} King End Offset: {(kingEnd ? 4 : 0)}  --  {values[index / 2] & (mask << offset)}");
        int result = (int)((values[index / 2] >> offset) & 0b1111);
        return (result - 8) * 5;
    }

    // private int CalculateEGScore(Board board)
    // {
    //     var lists = board.GetAllPieceLists();
    //     int[] endGameMults = { 0, 0, 2, 2, 1, 3, 0 };
    //     int egScore = 0;
    // 
    //     for (int i = 0; i < lists.Length; i++)
    //     {
    //         var list = lists[i];
    //         egScore += endGameMults[(int)list.TypeOfPieceInList] * list.Count;
    //     }
    // 
    //     return egScore;
    // }

    public int CalculateMaterial(Board board, bool topLevelLog = false)
    {
        int eval = 0;
        var lists = board.GetAllPieceLists();

        foreach (var list in lists)
        {
            eval += PieceWorth(list.TypeOfPieceInList) * list.Count * BoolToMult(list.IsWhitePieceList);
            // foreach (var piece in list) eval += PiecePosScore(piece) * BoolToMult(list.IsWhitePieceList);
            // egScore += endGameMults[(int)list.TypeOfPieceInList] * list.Count;
        }

        // var isEg = totalMat < (900 + 900 + 500 + 500 + 300 + 10000) * 2;
        // Console.WriteLine(totalMat);
        // Console.WriteLine(resultStr);
        // foreach (var list in lists)
        //     foreach (var piece in list) eval += PiecePosScore(piece, isEg) * BoolToMult(list.IsWhitePieceList);

        return eval;
    }
}