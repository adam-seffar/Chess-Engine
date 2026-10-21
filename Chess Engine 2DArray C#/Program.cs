using Chess_Engine_2DArray_C_;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "perftsuite")
        {
            Environment.Exit(PerftSuite.Run() ? 0 : 1);
        }

        // Any other arguments run as a single command, e.g. `ChessEngine bench 8`.
        var uci = new Uci();
        if (args.Length > 0)
            uci.Execute(string.Join(' ', args));
        else
            uci.Run();
    }
}
