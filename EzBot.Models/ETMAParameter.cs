namespace EzBot.Models;

public class ETMAParameter : Parameter
{

    public int WindowSize { get; set; }
    public double Offset { get; set; }
    public double Sigma { get; set; }


    public ETMAParameter(string id, int windowSize, double offset, double sigma)
    {
        Id = id;
        WindowSize = windowSize;
        Offset = offset;
        Sigma = sigma;
    }

}
