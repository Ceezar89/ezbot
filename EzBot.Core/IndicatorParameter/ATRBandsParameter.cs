namespace EzBot.Core.IndicatorParameter;

public class AtrBandsParameter : IndicatorParameterBase
{
    private int _period = 14;
    private double _multiplier = 2.0;
    private double _riskRewardRatio = 1.1;

    // Ranges
    private static readonly (int Min, int Max) PeriodRange = (12, 12);
    private static readonly (double Min, double Max) MultiplierRange = (2.0, 3.0);
    private static readonly (double Min, double Max) RiskRewardRatioRange = (1.1, 2.0);

    // Steps
    private const int PeriodRangeStep = 2;
    private const double MultiplierRangeStep = 0.5;
    private const double RiskRewardRatioRangeStep = 0.1;

    // Correctly calculate the number of steps for each parameter range
    private static readonly int PeriodPermutations = CalculateSteps(PeriodRange.Min, PeriodRange.Max, PeriodRangeStep);
    private static readonly int MultiplierPermutations = CalculateSteps(MultiplierRange.Min, MultiplierRange.Max, MultiplierRangeStep);
    private static readonly int RiskRewardRatioPermutations = CalculateSteps(RiskRewardRatioRange.Min, RiskRewardRatioRange.Max, RiskRewardRatioRangeStep);

    public static readonly int TotalPermutations = PeriodPermutations * MultiplierPermutations * RiskRewardRatioPermutations;

    // Type identifier for AtrBandsParameter (unique across all parameter types)
    private const byte TYPE_ID = 0x01;

    protected override byte GetTypeIdentifier() => TYPE_ID;

    protected override byte[] GetParameterSpecificBinaryData()
    {
        byte[] data = new byte[16]; // 4 bytes for Period, 8 bytes for Multiplier, 8 bytes for RiskRewardRatio

        // Store Period (4 bytes)
        BitConverter.GetBytes(_period).CopyTo(data, 0);

        // Store Multiplier (8 bytes)
        BitConverter.GetBytes(_multiplier).CopyTo(data, 4);

        // Store RiskRewardRatio (8 bytes)
        BitConverter.GetBytes(_riskRewardRatio).CopyTo(data, 12);

        return data;
    }

    /// <summary>
    /// Updates the instance fields from the parameter-specific binary data.
    /// </summary>
    /// <param name="data">Byte array containing only the specific data for this parameter type.</param>
    /// <exception cref="ArgumentException">Thrown if data length is incorrect.</exception>
    protected override void UpdateFromParameterSpecificBinaryData(byte[] data)
    {
        if (data.Length != 16) // Ensure the data length matches serialization format
            throw new ArgumentException("Invalid data length for AtrBandsParameter update", nameof(data));

        // Update fields using properties to leverage validation logic
        Period = BitConverter.ToInt32(data, 0);
        Multiplier = BitConverter.ToDouble(data, 4);
        RiskRewardRatio = BitConverter.ToDouble(data, 12);
    }

    // Static method to create AtrBandsParameter from binary data
    public static AtrBandsParameter FromBinary(string name, byte[] data)
    {
        if (data.Length != 16)
            throw new ArgumentException("Invalid data length for AtrBandsParameter");

        int period = BitConverter.ToInt32(data, 0);
        double multiplier = BitConverter.ToDouble(data, 4);
        double riskRewardRatio = BitConverter.ToDouble(data, 12);

        return new AtrBandsParameter(name, period, multiplier, riskRewardRatio);
    }

    // Register the type with the factory method
    static AtrBandsParameter()
    {
        // This will be called when the class is first used
        RegisterType();
    }

    private static void RegisterType()
    {
        // Register this type with the factory
        IndicatorParameterBase.RegisterParameterType(TYPE_ID, (name, data) => AtrBandsParameter.FromBinary(name, data));
    }

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_period, PeriodRange.Min, PeriodRange.Max, PeriodRangeStep, "Period"),
            new ParameterDescriptor(_multiplier, MultiplierRange.Min, MultiplierRange.Max, MultiplierRangeStep, "Multiplier"),
            new ParameterDescriptor(_riskRewardRatio, RiskRewardRatioRange.Min, RiskRewardRatioRange.Max, RiskRewardRatioRangeStep, "Risk Reward Ratio")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Period":
                Period = (int)descriptor.Value;
                break;
            case "Multiplier":
                Multiplier = (double)descriptor.Value;
                break;
            case "Risk Reward Ratio":
                RiskRewardRatio = (double)descriptor.Value;
                break;
        }
    }


    public int Period
    {
        get => _period;
        set => ValidateAndSetValue(ref _period, value, PeriodRange);
    }

    public double Multiplier
    {
        get => _multiplier;
        set => ValidateAndSetValue(ref _multiplier, value, MultiplierRange);
    }

    public double RiskRewardRatio
    {
        get => _riskRewardRatio;
        set => ValidateAndSetValue(ref _riskRewardRatio, value, RiskRewardRatioRange);
    }

    public AtrBandsParameter() : base("atr_bands")
    {
        Period = PeriodRange.Min;
        Multiplier = MultiplierRange.Min;
        RiskRewardRatio = RiskRewardRatioRange.Min;
    }

    public AtrBandsParameter(string name, int period, double multiplier, double riskRewardRatio)
        : base(name)
    {
        Period = period;
        Multiplier = multiplier;
        RiskRewardRatio = riskRewardRatio;
    }

    public override bool IncrementSingle()
    {
        if (IncrementValue(ref _riskRewardRatio, RiskRewardRatioRangeStep, RiskRewardRatioRange))
            return true;
        _riskRewardRatio = RiskRewardRatioRange.Min;

        if (IncrementValue(ref _multiplier, MultiplierRangeStep, MultiplierRange))
            return true;
        _multiplier = MultiplierRange.Min;

        if (IncrementValue(ref _period, PeriodRangeStep, PeriodRange))
            return true;

        return false;
    }

    public override void Reset()
    {
        Period = PeriodRange.Min;
        Multiplier = MultiplierRange.Min;
        RiskRewardRatio = RiskRewardRatioRange.Min;
    }

    public override AtrBandsParameter DeepClone()
    {
        return new AtrBandsParameter(Name, Period, Multiplier, RiskRewardRatio);
    }

    public override AtrBandsParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int periodSteps = (PeriodRange.Max - PeriodRange.Min) / PeriodRangeStep + 1;
        int multiplierSteps = (int)Math.Floor((MultiplierRange.Max - MultiplierRange.Min) / MultiplierRangeStep) + 1;
        int riskRewardRatioSteps = (int)Math.Floor((RiskRewardRatioRange.Max - RiskRewardRatioRange.Min) / RiskRewardRatioRangeStep) + 1;

        // Choose a random step for each parameter
        var period = PeriodRange.Min + (random.Next(periodSteps) * PeriodRangeStep);
        var multiplier = MultiplierRange.Min + (random.Next(multiplierSteps) * MultiplierRangeStep);
        var riskRewardRatio = RiskRewardRatioRange.Min + (random.Next(riskRewardRatioSteps) * RiskRewardRatioRangeStep);

        return new AtrBandsParameter(Name, period, multiplier, riskRewardRatio);
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(Period, Multiplier, RiskRewardRatio);
    }
}
