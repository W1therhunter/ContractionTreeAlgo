public class ChainConstructor
{
    public int tensorCount = 3;
    public int legCount = 4;

    public Tensor[] tc;

    //create simple Tensor-Network
    public ChainConstructor()
    {
        tc = new Tensor[tensorCount];
        for (int i = 0; i < tensorCount; i++)
        {
            tc[i] = new Tensor();
        }
        for (int i = 0; i < tensorCount - 1; i++)
        {
            Leg l = new Leg(5);

            tc[i].Legs.Add(l);
            tc[i + 1].Legs.Add(l);
        }
    }
    //create Tensor chain with given parameters
    public ChainConstructor(int tensorCount, int maxSize, int minRank)
    {
        if (tensorCount < 1 || maxSize < 1 || minRank < 1)
        {
            tc = new Tensor[tensorCount];
            for (int i = 0; i < tensorCount; i++)
            {
                tc[i] = new Tensor();
            }
            for (int i = 0; i < tensorCount - 1; i++)
            {
                Leg l = new Leg(5);

                tc[i].Legs.Add(l);
                tc[i + 1].Legs.Add(l);
            }
        }
        else
        {
            this.tensorCount = tensorCount;
            tc = new Tensor[tensorCount];
            List<Tensor> connected = new List<Tensor>();
            for (int i = 0; i < tensorCount; i++)
            {

                var rnd = new Random();

                tc[i] = new Tensor();
                if (i > 0)
                {
                    var ttoconnect = connected[rnd.Next(connected.Count)];
                    int sizeLeg = rnd.Next(1, maxSize);
                    var l = new Leg(sizeLeg);
                    tc[i].Legs.Add(l);
                    ttoconnect.Legs.Add(l);
                }

                connected.Add(tc[i]);

                for (int j = 0; j < minRank; j++)
                {
                    var k = new Leg(rnd.Next(1, maxSize));
                    tc[i].Legs.Add(k);
                }
            }
        }

    }

}