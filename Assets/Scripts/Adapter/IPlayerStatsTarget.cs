public interface IPlayerStatsTarget
{
    int PlayerId { get; }
    
    void ApplyGlobalStatMultiplier(float mult);
    
    void ApplyConditionalSpeed();
}