using System.Collections.Concurrent;

/// <summary>
/// This tries to implement all the parallelisation possibilities of the respective algorithm. Unfortunatly it's slow, due to overhead.
/// </summary>
public class TreeOptimizerA
{
    //for the shared there is int[] bc we have the parameter k determing the shared edges, when we select Tensor k
    private ConcurrentDictionary<(int, int), double[]> sharedCached = new ConcurrentDictionary<(int, int), double[]>();
    private ConcurrentDictionary<(int, int), double> outsizeCached = new ConcurrentDictionary<(int, int), double>();

    private async Task<double> CalcCostAsync(CongestionCost mode, double outSize, double sharedSize)
    {
        double r = 0.0;
        await Task.Run(() =>
        {
            if (mode == CongestionCost.Edge)
            {
                r = Math.Log2(outSize);
            }
            else
            {
                r = Math.Log(outSize, sharedSize);
            }
        }
        );
        return r;
        //Cost functions according to the paper

    }
    private double[] CalcSharedRequire(Tensor[] tensorChain, int i, int j)
    {
        
        double[] shared = new double[j];
        double oldSum = 0.0;
        double newSum = 0.0;
        for (int k = i; k < j; k++)
        {
            //Here we split up the upper and the lower half of the chain 
            HashSet<Leg> upper = new HashSet<Leg>();
            try
            {
                foreach (Tensor t in tensorChain[i..(k - 1)])
                {
                    upper.UnionWith(t.Legs);
                }
                var old = tensorChain[k].Legs
                .Where(a => upper.Contains(a))
                .Sum(a => a.Size);
                oldSum = old;
            }
            //if k-1 < i, it may throw IndexOutOfBounds
            catch (System.Exception)
            {
                oldSum = 0.0;
            }
            HashSet<Leg> lower = new HashSet<Leg>();
            try
            {
                foreach (Tensor t in tensorChain[(k + 1)..j])
                {
                    lower.UnionWith(t.Legs);
                }
                var niew = tensorChain[k].Legs
                .Where(a => lower.Contains(a))
                .Sum(a => a.Size);
                newSum = niew;
            }
            //if k+1 >= j, it may throw IndexOutOfBounds
            catch (System.Exception)
            {
                newSum = 0.0;
            }
            foreach (Tensor t in tensorChain[(k + 1)..j])
            {
                lower = lower.Concat(t.Legs).ToHashSet();
            }

            //In case the List is empty we cannot take the last value of the list obviously
            shared[k] += newSum - oldSum;
            if (k != j - 1)
            {
                shared[k + 1] = shared[k];
            }
        }
        return shared;
    }

    /// <summary>
    /// Calculates the Size of the resulting Tensor, when Tensor i to j are contracted
    /// </summary>
    /// <param name="tensorChain">the complete Tensor Chain</param>
    /// <param name="i">the first index</param>
    /// <param name="j">the last index</param>
    /// <returns>Size of Tensor</returns>
    private double Outsize(Tensor[] tensorChain, int i, int j)
    {
        HashSet<Leg> legs = new HashSet<Leg>();
        //if we only have one Tensor
        if (i == j && i < tensorChain.Length)
        {
            foreach (Leg l in tensorChain[i].Legs)
            {
                if (!legs.Contains(l))
                {
                    legs.Add(l);
                }
            }
        }
        //This calculates the Product of all the Dimensions sizes 
        else
        {
            for (int k = i; k < j; k++)
            {
                foreach (Leg l in tensorChain[k].Legs)
                {
                    if (!legs.Contains(l))
                    {
                        //if we found a dimension we don't already have, store it
                        legs.Add(l);
                    }
                    else
                    {
                        //we found one leg twice, so we remove it, as it will not effect the calculation of X_i,j Size
                        legs.Remove(l);
                    }
                }
            }
        }

        //Multiply all the sizes of the legs
        var os = legs.Aggregate(1.0, (result, leg) => result * leg.Size);
        return os;
    }




    /// <summary>
    /// This Method implements the algorithm with parallelisation to find the cost, but also the tree to get the right contraction order
    /// </summary>
    /// <param name="tensorChain">This is the Chain consisting of Tensors in a linear order</param>
    /// <param name="i">This is the start index</param>
    /// <param name="j">and this is the final index to find the cost, tree on X_ij</param>
    /// <param name="cc">this is a flag, the congestion cost mode (either edge or vertex)</param>
    /// <returns>Tupel consisting of the cost and the node for the tree</returns>


    public async Task<(double, Node?)> TreeStructureOptimizationAsync(Tensor[] tensorChain, int i, int j, CongestionCost cc)
    {
        //basecase
        if (i == j)
        {
            return (outsizeCached.GetOrAdd((i, i), Outsize(tensorChain, i, j)), new Node(null, i, null));
        }
        //calculating shared, but only for vertex
        var shared = new double[j];
        if (cc == CongestionCost.Vertex)
        {
            shared = sharedCached.GetOrAdd((i, j), CalcSharedRequire(tensorChain, i, j));
        }
        //calculating outSize
        var outSize = outsizeCached.GetOrAdd((i, j), Outsize(tensorChain, i, j));
        var cPrime = double.PositiveInfinity;
        Node tPrime = new Node(null, i, null);

        //storing all the returnvalues for later evaluation
        double[] cs = new double[j];
        (double, Node?)[] ls = new (double, Node?)[j];
        (double, Node?)[] rs = new (double, Node?)[j];

        async Task RunThreadsAsync(int i, int j, int k)
        {
            List<Task> tasks = new List<Task>();

            int index = k;
            Task cTask = Task.Run(async () =>
            {
                int insertindex = index - i;
                double c = await CalcCostAsync(cc, outSize, shared[index]);
                lock (cs)
                {
                    cs[insertindex] = c;
                }
            });

            Task lTask = Task.Run(async () =>
            {
                int insertindex = index - i;
                var left = await TreeStructureOptimizationAsync(tensorChain, i, index, cc);
                lock (ls)
                {
                    ls[insertindex] = left;
                }
            });

            Task rTask = Task.Run(async () =>
            {
                int insertindex = index - i;
                var right = await TreeStructureOptimizationAsync(tensorChain, index + 1, j, cc);
                lock (rs)
                {
                    rs[insertindex] = right;
                }
            });

            tasks.Add(cTask);
            tasks.Add(lTask);
            tasks.Add(rTask);
            await Task.WhenAll(tasks);
        }
        for (int k = 0; k < j - i; k++)
        {
            await RunThreadsAsync(i, j, k);
            double c = cs[k];
            if (c > cPrime) continue;

            (double cl, Node? l) = ls[k];
            //update to max
            c = cl > c ? cl : c;
            if (c > cPrime) continue;

            (double cr, Node? r) = rs[k];
            //update to max
            c = cr > c ? cr : c;

            //update to min (of k splits)
            if (c < cPrime)
            {
                cPrime = c;
                tPrime = new Node(l, k, r);
            }
        }

        return (cPrime, tPrime);
    }
}
