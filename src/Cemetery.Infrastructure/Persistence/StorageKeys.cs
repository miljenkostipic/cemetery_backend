namespace Cemetery.Infrastructure.Persistence;

/// <summary>
/// Column length for every file-storage key. Database keys are bare UUIDs (36 chars); R2 keys are
/// <c>{tenantId}/{uuid v7}</c> (73 chars). One length so no table can be narrower than the keys it holds.
/// </summary>
internal static class StorageKeys
{
    public const int MaxLength = 128;
}
