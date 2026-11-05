namespace DuskTwilight
{
    /// <summary>Puzzle state that a <see cref="StageSection"/> puts back when the player falls in it.</summary>
    public interface IResettable
    {
        void ResetState();
    }
}
