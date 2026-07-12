namespace UniDesk.Services;

public sealed class TemperatureSpikeFilter
{
    private const double SpikeThresholdCelsius = 40;
    private const double PendingToleranceCelsius = 5;
    private const int RequiredConfirmations = 3;
    private double? _acceptedValue;
    private double? _pendingValue;
    private int _pendingCount;
    private string? _source;

    public double? Apply(double? value, string? source)
    {
        if (!value.HasValue)
        {
            ResetPending();
            return null;
        }

        if (!_acceptedValue.HasValue || !string.Equals(_source, source, StringComparison.Ordinal))
        {
            _source = source;
            _acceptedValue = value;
            ResetPending();
            return value;
        }

        if (Math.Abs(value.Value - _acceptedValue.Value) <= SpikeThresholdCelsius)
        {
            _acceptedValue = value;
            ResetPending();
            return value;
        }

        if (_pendingValue.HasValue && Math.Abs(value.Value - _pendingValue.Value) <= PendingToleranceCelsius)
        {
            _pendingCount++;
            _pendingValue = value;
        }
        else
        {
            _pendingValue = value;
            _pendingCount = 1;
        }

        if (_pendingCount < RequiredConfirmations)
        {
            return _acceptedValue;
        }

        _acceptedValue = value;
        ResetPending();
        return value;
    }

    private void ResetPending()
    {
        _pendingValue = null;
        _pendingCount = 0;
    }
}
