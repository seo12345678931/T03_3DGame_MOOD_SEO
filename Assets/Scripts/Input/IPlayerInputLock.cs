namespace Mood.Input
{
    public interface IPlayerInputLock
    {
        bool IsInputLocked { get; }
        bool TryLockInput(object owner);
        void UnlockInput(object owner);
    }
}
