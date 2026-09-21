namespace IncidentAgent.Persistence;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public bool Enabled { get; init; }

    public string ConnectionString { get; init; } =
        "Host=localhost;Port=5432;Database=incident_agent;Username=incident_agent;Password=incident_agent";
}
