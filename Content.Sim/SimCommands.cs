using Content.Shared.Commands;

namespace Content.Sim;

public static class SimCommands
{
    public static void Apply(SimRuntime sim, SimCommand command, string factionId = SimRuntime.DefaultFaction)
    {
        switch (command)
        {
            case MoveCommand move:
                sim.IssueMoveOrder(move.UnitName, move.Destination, factionId);
                break;
            case SpawnCommand spawn:
                sim.SpawnFromPrototype(spawn.PrototypeId, spawn.Position, spawn.Velocity);
                break;
            case EngageCommand engage:
                sim.IssueEngagement(engage.UnitName, engage.TrackId, engage.Weapon, engage.Count, factionId);
                break;
            case PostureCommand posture:
                sim.SetPosture(posture.UnitName, posture.Posture, factionId);
                break;
            case LockCommand lockOrder:
                sim.SetLock(lockOrder.UnitName, lockOrder.TrackId, factionId);
                break;
            case TeleportCommand teleport:
                sim.DebugSetPosition(teleport.EntityId, teleport.Position);
                break;
            case AiCommand ai:
                sim.SetAiEnabled(ai.Enabled);
                break;
        }
    }
}
