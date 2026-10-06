namespace GWYF_CasinoChaos
{
    internal enum MafiaGuardState { Idle, Chasing, Attacking, Recovering, Defeated }

    internal sealed class MafiaGuardLifecycle
    {
        internal const float DefeatedLifetimeSeconds = 60f;
        internal MafiaGuardState State { get; private set; }
        internal double DespawnAt { get; private set; }
        internal bool IsDefeated => State == MafiaGuardState.Defeated;

        internal bool TryTransition(MafiaGuardState next, double now)
        {
            if (IsDefeated || State == next) return false;
            State = next;
            if (IsDefeated) DespawnAt = now + DefeatedLifetimeSeconds;
            return true;
        }

        internal bool ShouldDespawn(double now) => IsDefeated && now >= DespawnAt;
    }
}
