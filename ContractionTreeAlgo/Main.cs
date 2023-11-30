using System.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        bool useThreads = false;
        bool measureTime = false;
        int tensorCount = 10;
        int maxLegSize = 30;
        int minRank = 4;
        var congestion = CongestionCost.Edge;

        foreach (string arg in args)
        {
            switch (arg)
            {
                case "-t":
                    useThreads = true;
                    break;
                case "-b":
                    measureTime = true;
                    break;
                case "-c":
                    string costMode = args[Array.IndexOf(args, arg) + 1].ToLower();
                    if (costMode == "edge")
                    {
                        congestion = CongestionCost.Edge;
                    }
                    if (costMode == "vertex")
                    {
                        congestion = CongestionCost.Vertex;
                    }
                    else
                    {
                        Console.WriteLine("Allowed Cost Modes are \"edge\" and \"vertex\"");
                        return;
                    }
                    break;
                case "-tc":
                    try
                    {
                        tensorCount = Convert.ToInt32(args[Array.IndexOf(args, arg) + 1]);
                    }
                    catch
                    {
                        Console.WriteLine("Tensor Count must be int");
                        return;
                    }
                    break;
                case "-maxS":
                    try
                    {
                        maxLegSize = Convert.ToInt32(args[Array.IndexOf(args, arg) + 1]);
                    }
                    catch
                    {
                        Console.WriteLine("Max Leg Size must be int");
                        return;
                    }
                    break;
                case "-minR":
                    try
                    {
                        minRank = Convert.ToInt32(args[Array.IndexOf(args, arg) + 1]);
                    }
                    catch
                    {
                        Console.WriteLine("Min Rank must be int");
                        return;
                    }
                    break;
                case "--help":
                    Console.WriteLine("Help:");
                    Console.WriteLine("-t: Use threads");
                    Console.WriteLine("-b: Measure time");
                    Console.WriteLine("-c: edge|vertex Cost mode");
                    Console.WriteLine("-tc: Number of tensors");
                    Console.WriteLine("-maxS: Maximum leg size");
                    Console.WriteLine("-minR: Minimum rank");
                    break;
                default:
                    Console.WriteLine($"{arg} is not an argument, type \"--help\" for more information");
                    break;
            }
        }
        Console.WriteLine("Arguments evaluated:");
        Console.WriteLine("-t: " + useThreads);
        Console.WriteLine("-b: " + measureTime);
        Console.WriteLine("-c: " + congestion);
        Console.WriteLine("-tc: " + tensorCount);
        Console.WriteLine("-maxS: " + maxLegSize);
        Console.WriteLine("-minR: " + minRank);

        ChainConstructor cc = new ChainConstructor(tensorCount, maxLegSize, minRank);
        if (measureTime)
        {
            TreeOptimizerT tt = new TreeOptimizerT();
            TreeOptimizer t = new TreeOptimizer();
            Stopwatch sw = new Stopwatch();

            //Measure Time with Threads
            sw.Start();
            (double cost, Node? tree) = tt.TreeStructureOptimization(cc.tc, 0, cc.tc.Length, congestion);
            sw.Stop();
            Console.WriteLine($"Running the parallelised version took {sw.ElapsedMilliseconds} milliseconds");
            Console.WriteLine($"The {congestion}-congestion cost is {cost}");
            Console.WriteLine("The contraction order is given by " + tree.ToString());

            //Measure Time without Threads
            sw.Reset();
            (cost, tree) = t.TreeStructureOptimization(cc.tc, 0, cc.tc.Length, congestion);
            sw.Start();
            Console.WriteLine($"Running the unparallelised version took {sw.ElapsedMilliseconds} milliseconds");
            Console.WriteLine($"The {congestion}-congestion cost is {cost}");
            Console.WriteLine("The contraction order is given by " + tree.ToString());

            return;
        }
        if (useThreads)
        {
            TreeOptimizerT t = new TreeOptimizerT();
            (double cost, Node? tree) = t.TreeStructureOptimization(cc.tc, 0, cc.tc.Length, congestion);
            Console.WriteLine($"The {congestion}-congestion cost is {cost}");
            Console.WriteLine("The contraction order is given by " + tree.ToString());
            return;
        }
        else
        {
            TreeOptimizer t = new TreeOptimizer();
            (double cost, Node? tree) = t.TreeStructureOptimization(cc.tc, 0, cc.tc.Length, congestion);
            Console.WriteLine($"The {congestion}-congestion cost is {cost}");
            Console.WriteLine("The contraction order is given by " + tree.ToString());
            return;
        }
    }
}
