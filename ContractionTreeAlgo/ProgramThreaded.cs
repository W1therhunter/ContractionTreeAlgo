using System.Collections.Concurrent;

public class TreeOptimizerT
{
    //Caches for Shared and Outsize. Only usefull when calculating more layers
    private ConcurrentDictionary<(int, int), double[]> sharedCached = new ConcurrentDictionary<(int, int), double[]>();
    private ConcurrentDictionary<(int, int), double> outsizeCached = new ConcurrentDictionary<(int, int), double>();

    /// <summary>
    /// Function to calculate the respective costs
    /// </summary>
    /// <param name="mode">either Vertex or Edge</param>
    /// <param name="outSize">Size of the resulting Tensor</param>
    /// <param name="shared">Shared legs of the left and the right part</param>
    /// <returns>cost for the specific contraction</returns>
    private double CalcCost(CongestionCost mode, double outSize, double shared)
    {
        //Cost functions according to the paper
        if (mode == CongestionCost.Edge)
        {
            return Math.Log2(outSize);
        }
        else
        {
            return Math.Log(outSize, shared);
        }
    }
    /// <summary>
    /// Function to calculate the shared 
    /// </summary>
    /// <param name="tensorChain">Linear Arrangement of the Tensors to contract</param>
    /// <param name="i">starting index</param>
    /// <param name="j">ending index</param>
    /// <returns>shared values for any k between i and j</returns>
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


    public (double, Node?) TreeStructureOptimization(Tensor[] tensorChain, int i, int j, CongestionCost cc)
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

        //lock object, in case cPrime and tPrime needs to be updated
        object locko = new object();

        //First layer runs Parallel, but for every k the function will continue recursively
        Parallel.For(i, j, k =>
        {
            double c = CalcCost(cc, outSize, shared[k]);
            if (c > cPrime)
            {
                return;
            }
            TreeOptimizer to = new TreeOptimizer();
            (double cl, Node? l) = to.TreeStructureOptimization(tensorChain, i, k, cc);
            //update to max
            c = cl > c ? cl : c;
            if (c > cPrime)
            {
                return;
            }
            (double cr, Node? r) = to.TreeStructureOptimization(tensorChain, k + 1, j, cc);
            //update to max
            c = cr > c ? cr : c;

            //update to min (of k splits)
            lock (locko)
            {
                if (c < cPrime)
                {
                    cPrime = c;
                    tPrime = new Node(l, k, r);
                }

            }
        });

        return (cPrime, tPrime);
    }
}
