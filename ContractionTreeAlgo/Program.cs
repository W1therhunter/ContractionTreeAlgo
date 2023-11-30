/// <summary>
/// Modeling Legs as a class, so HashMap can destinguish between the Legs
/// </summary>
public class Leg
{
    public double Size { get; set; }

    public Leg(double size)
    {
        Size = size;
    }
}
/// <summary>
/// Tensor consiting of multiple Legs
/// </summary>
public class Tensor
{
    public HashSet<Leg> Legs { get; set; }

    public Tensor()
    {
        Legs = new HashSet<Leg>();
    }
}

[Flags]
public enum CongestionCost
{
    Edge, Vertex
}
/// <summary>
/// To model the Tree
/// </summary>
public class Node
{
    public Node? left, right;
    public int Value;

    public Node(Node? l, int Value, Node? r)
    {
        this.left = l;
        this.right = r;
        this.Value = Value;
    }

    public override string ToString()
    {
        if (right == null && left == null)
        {
            return $"X_{Value}";
        }
        String l = left == null ? "" : "(" + left.ToString() + ")";
        String r = right == null ? "" : "(" + right.ToString() + ")";
        return $"{l}{r}";
    }
}
public class TreeOptimizer
{
    //for the shared there is int[] bc we have the parameter k determing the shared edges, when we select Tensor k
    private Dictionary<(int, int), double[]> sharedCached = new Dictionary<(int, int), double[]>();
    private Dictionary<(int, int), double> outsizeCached = new Dictionary<(int, int), double>();

    /// <summary>
    /// Function to calculate the respective costs
    /// </summary>
    /// <param name="mode">either Vertex or Edge</param>
    /// <param name="outSize">Size of the resulting Tensor</param>
    /// <param name="shared">Shared legs of the left and the right part</param>
    /// <returns>cost for the specific contraction</returns>
    private double CalcCost(CongestionCost mode, double outSize, double sharedSize)
    {
        //Cost functions according to the paper
        if (mode == CongestionCost.Edge)
        {
            return Math.Log2(outSize);
        }
        else
        {
            return Math.Log(outSize, sharedSize);
        }
    }

    /// <summary>
    /// Calculates the SharedLegs between index i and j of the Tensor
    /// </summary>
    /// <param name="tensorChain"></param>
    /// <param name="i"></param>
    /// <param name="j"></param>
    /// <returns></returns>
    private double[] CalcSharedRequire(Tensor[] tensorChain, int i, int j)
    {
        //first lookup in shareCached if we have a value already
        try
        {
            return sharedCached[(i, j)];
        }
        catch (KeyNotFoundException)
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
                        upper = upper.Concat(t.Legs).ToHashSet();
                    }
                    var old = tensorChain[k].Legs
                    .Where(a => upper.Contains(a))
                    .Sum(a => a.Size);
                    oldSum = old;
                }
                //if k-1 < i, it may throw IndexOutOfBounds
                catch (IndexOutOfRangeException)
                {
                    oldSum = 0.0;
                }
                HashSet<Leg> lower = new HashSet<Leg>();
                try
                {
                    foreach (Tensor t in tensorChain[(k + 1)..j])
                    {
                        lower = lower.Concat(t.Legs).ToHashSet();
                    }
                    var niew = tensorChain[k].Legs
                    .Where(a => lower.Contains(a))
                    .Sum(a => a.Size);
                    newSum = niew;
                }
                //if k+1 >= j, it may throw IndexOutOfBounds
                catch (IndexOutOfRangeException)
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
            sharedCached.Add((i, j), shared);
            return shared;
        }


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
        
        //first check if outsize(i,j) is cached
        try
        {
            return outsizeCached[(i, j)];
        }
        //if not calculate the Size
        catch(KeyNotFoundException)
        {
            HashSet<Leg> legs = new HashSet<Leg>();
            //This calculates the Product of all the Dimensions sizes
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
            outsizeCached.Add((i, j), os);
            return os;
        }

    }

    /// <summary>
    /// This Method implements the algorithm to find the cost , but also the tree to get the right contraction order
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
            return (Outsize(tensorChain, i, j), new Node(null, i, null));
        }
        //calculating shared, but only for vertex
        var shared = new double[j];
        if (cc == CongestionCost.Vertex)
        {
            shared = CalcSharedRequire(tensorChain, i, j);
        }
        //calculating outSize
        var outSize = Outsize(tensorChain, i, j);

        var cPrime = double.PositiveInfinity;
        Node tPrime = new Node(null, i, null);

        for (int k = i; k < j; k++)
        {
            double c = CalcCost(cc, outSize, shared[k]);
            if (c > cPrime)
            {
                continue;
            }
            (double cl, Node? l) = TreeStructureOptimization(tensorChain, i, k, cc);
            //update to max
            c = cl > c ? cl : c;
            if (c > cPrime)
            {
                continue;
            }
            (double cr, Node? r) = TreeStructureOptimization(tensorChain, k + 1, j, cc);
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



