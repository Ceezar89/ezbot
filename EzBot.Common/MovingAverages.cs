namespace EzBot.Common;
public static class MovingAverages
{
    // Simple Moving Average
    public static List<double> SMA(List<double> values, int period)
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

    // Exponential Moving Average
    public static List<double> EMA(List<double> values, int period)
    {
        int count = values.Count;
        List<double> ema = new List<double>(new double[count]);
        double multiplier = 2.0 / (period + 1);
        double sum = 0.0;

        for (int i = 0; i < count; i++)
        {
            sum += values[i];

            if (i >= period - 1)
            {
                if (i == period - 1)
                {
                    ema[i] = sum / period;
                }
                else
                {
                    ema[i] = (values[i] - ema[i - 1]) * multiplier + ema[i - 1];
                }
            }
            else
            {
                ema[i] = 0.0; // Insufficient data for EMA
            }
        }
        return ema;
    }

    // calculate Arnaud Legoux Moving Average
    public static List<double> ALMA(List<double> data, int windowSize, double offset, double sigma)
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
