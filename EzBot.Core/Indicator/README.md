# EzBot.Core.Indicator

This namespace implements various technical indicators and risk management tools for the EzBot trading system.

## Components

- **IndicatorBase**: Abstract base class for all indicators.
- **IIndicator**: Interface definitions, including volume, trend, and risk management.
- **Trend Indicators**
  - **Trendilo.cs**: Computes trend signals using percentage change and ALMA.
  - **Etma.cs**: Implements a trend indicator using a weighted moving average.
- **Volume Indicator**
  - **NormalizedVolume.cs**: Normalizes volume against its simple moving average.
- **Risk Management**
  - **ATRBands.cs**: Calculates stop loss levels based on ATR.
- **IndicatorCollection.cs**: Manages collections of indicators.

## Folder Structure

```
/EzBot.Core/Indicator
   ├── IndicatorBase.cs
   ├── IIndicator.cs
   ├── IndicatorCollection.cs
   ├── Trendilo.cs
   ├── NormalizedVolume.cs
   ├── Etma.cs
   └── ATRBands.cs
```

## Usage

Indicators calculate signals based on bar data. Extend or integrate these components within your trading strategies for streamlined performance analysis.

## Contributing

Contributions and improvements are welcome. Please adhere to standard contribution guidelines.