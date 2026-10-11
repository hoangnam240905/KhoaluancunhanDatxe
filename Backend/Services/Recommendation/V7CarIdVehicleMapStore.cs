namespace Backend.Services.Recommendation;

/// <summary>
/// In-memory car_id → VehicleId map produced by Step 3.5 V7VehicleSeeder (no schema change).
/// </summary>
public sealed class V7CarIdVehicleMapStore
{
    private readonly object _gate = new();
    private Dictionary<string, int> _map = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, int> Map
    {
        get
        {
            lock (_gate)
                return _map;
        }
    }

    public void Replace(IReadOnlyDictionary<string, int> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        lock (_gate)
        {
            _map = new Dictionary<string, int>(map, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void MergeInto(IDictionary<string, int> target)
    {
        ArgumentNullException.ThrowIfNull(target);
        lock (_gate)
        {
            foreach (var (carId, vehicleId) in _map)
                target[carId] = vehicleId;
        }
    }
}
