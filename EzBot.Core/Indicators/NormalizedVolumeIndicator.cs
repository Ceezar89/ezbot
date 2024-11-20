namespace EzBot.Core.Indicators;

public class NormalizedVolumeIndicator
{
    // Inputs
    public int Length { get; set; } = 50; // Volume Period
    public int HighVolumeThreshold { get; set; } = 150; // High Volume
    public int NormalVolumeThreshold { get; set; } = 75; // Normal Volume Upper Limit
    public int LowVolumeThreshold { get; set; } = 75; // Low Volume
    public bool BarColor { get; set; } = true; // Enable Bar Coloring

    // Output series
    public List<double> NormalizedVolume { get; private set; } = new List<double>();
    public List<string> Colors { get; private set; } = new List<string>();
    public List<string> BarColors { get; private set; } = new List<string>();

    // Constructor
    public NormalizedVolumeIndicator(int length = 50, int hv = 150, int nv = 75, int lv = 75, bool barColor = true)
    {
        Length = length;
        HighVolumeThreshold = hv;
        NormalVolumeThreshold = nv;
        LowVolumeThreshold = lv;
        BarColor = barColor;
    }

    // Calculation method
    public void Calculate(List<BarData> bars)
    {
        int count = bars.Count;

        // Clear output lists
        NormalizedVolume.Clear();
        Colors.Clear();
        BarColors.Clear();

        // Extract volumes
        List<double> volumes = bars.Select(b => b.Volume).ToList();

        // Calculate SMA of volume
        List<double> smaVolume = SMA(volumes, Length);

        for (int i = 0; i < count; i++)
        {
            double volume = volumes[i];
            double smaVol = smaVolume[i];

            // Avoid division by zero
            if (smaVol == 0)
            {
                NormalizedVolume.Add(0.0);
            }
            else
            {
                NormalizedVolume.Add((volume / smaVol) * 100.0);
            }

            // Assign colors based on normalized volume
            double nVol = NormalizedVolume[i];
            string color = string.Empty;

            if (nVol >= HighVolumeThreshold)
            {
                color = "#008000"; // Green
            }
            else if (nVol > NormalVolumeThreshold && nVol < HighVolumeThreshold)
            {
                color = "#FFFF00"; // Yellow
            }
            else if (nVol <= LowVolumeThreshold)
            {
                color = "#FF0000"; // Red
            }
            else
            {
                color = string.Empty; // No color
            }

            Colors.Add(color);

            // Assign bar colors if BarColor is true
            if (BarColor)
            {
                BarColors.Add(color);
            }
            else
            {
                BarColors.Add(string.Empty);
            }
        }
    }

    // Simple Moving Average function
    private List<double> SMA(List<double> values, int period)
    {
        int count = values.Count;
        List<double> sma = new List<double>(new double[count]);
        double sum = 0.0;

        for (int i = 0; i < count; i++)
        {
            sum += values[i];

            if (i >= period - 1)
            {
                if (i >= period)
                {
                    sum -= values[i - period];
                }
                sma[i] = sum / period;
            }
            else
            {
                sma[i] = 0.0; // Insufficient data for SMA
            }
        }

        return sma;
    }
}