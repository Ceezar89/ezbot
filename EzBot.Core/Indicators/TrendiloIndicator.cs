namespace EzBot.Core.Indicators;

public class Trendilo
{
    // Inputs
    public List<double> Src { get; set; } // Source data (e.g., closing prices)
    public int Smooth { get; set; } = 1; // Smoothing period
    public int Length { get; set; } = 50; // Lookback period for ALMA
    public double Offset { get; set; } = 0.85; // ALMA Offset
    public int Sigma { get; set; } = 6; // ALMA Sigma
    public double Bmult { get; set; } = 1.0; // Band Multiplier
    public bool Cblen { get; set; } = false; // Custom Band Length?
    public int Blen { get; set; } = 20; // Custom Band Length
    public bool Highlight { get; set; } = true; // Highlight plot?
    public bool Fill { get; set; } = true; // Fill areas?
    public bool Barcol { get; set; } = false; // Change bar color?

    // Output series
    public List<double> AvPch { get; private set; } = new List<double>(); // ALMA of percentage change
    public List<double> Rms { get; private set; } = new List<double>(); // RMS values
    public List<int> Cdir { get; private set; } = new List<int>(); // Current direction
    public List<string> Col { get; private set; } = new List<string>(); // Colors based on direction

    // Constructor
    public Trendilo(List<double> src)
    {
        Src = src;
    }

    // Calculation method
    public void Calculate()
    {
        int count = Src.Count;

        // Initialize lists with default values
        AvPch = new List<double>(new double[count]);
        Rms = new List<double>(new double[count]);
        Cdir = new List<int>(new int[count]);
        Col = new List<string>(new string[count]);

        // Calculate percentage change (pch)
        List<double> pch = new List<double>(new double[count]);
        for (int i = 0; i < count; i++)
        {
            if (i >= Smooth)
            {
                double srcChange = Src[i] - Src[i - Smooth];
                pch[i] = (srcChange / Src[i]) * 100.0;
            }
            else
            {
                pch[i] = 0.0; // Not enough data to compute change
            }
        }

        // Calculate ALMA of percentage change (AvPch)
        AvPch = CalculateALMA(pch, Length, Offset, Sigma);

        // Determine band length (blength)
        int blength = Cblen ? Blen : Length;

        // Calculate RMS values
        for (int i = 0; i < count; i++)
        {
            if (i >= blength - 1)
            {
                double sumSq = 0.0;
                for (int j = i - blength + 1; j <= i; j++)
                {
                    sumSq += AvPch[j] * AvPch[j];
                }
                Rms[i] = Bmult * Math.Sqrt(sumSq / blength);
            }
            else
            {
                Rms[i] = 0.0; // Not enough data to compute RMS
            }
        }

        // Calculate current direction (Cdir) and colors (Col)
        for (int i = 0; i < count; i++)
        {
            if (AvPch[i] > Rms[i])
            {
                Cdir[i] = 1;
            }
            else if (AvPch[i] < -Rms[i])
            {
                Cdir[i] = -1;
            }
            else
            {
                Cdir[i] = 0;
            }

            // Assign colors based on direction
            if (Cdir[i] == 1)
                Col[i] = "lime";
            else if (Cdir[i] == -1)
                Col[i] = "red";
            else
                Col[i] = "gray";
        }
    }

    // ALMA calculation method
    public List<double> CalculateALMA(List<double> data, int windowSize, double offset, double sigma)
    {
        int count = data.Count;
        List<double> alma = new List<double>(new double[count]);

        double m = offset * (windowSize - 1);
        double s = windowSize / sigma;

        for (int i = 0; i < count; i++)
        {
            if (i >= windowSize - 1)
            {
                double sum = 0.0;
                double sumw = 0.0;
                for (int j = 0; j < windowSize; j++)
                {
                    int idx = i - windowSize + 1 + j;
                    double weight = Math.Exp(-Math.Pow(j - m, 2) / (2 * Math.Pow(s, 2)));
                    sum += data[idx] * weight;
                    sumw += weight;
                }
                alma[i] = sum / sumw;
            }
            else
            {
                alma[i] = 0.0; // Not enough data to compute ALMA
            }
        }

        return alma;
    }
}
