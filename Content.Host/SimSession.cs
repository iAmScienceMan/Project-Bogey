using System;
using System.Collections.Generic;
using Content.Renderer.RealTime;
using Content.Shared.Commands;
using Content.Shared.Tracks;
using Content.Sim;

namespace Content.Host;

public sealed class SimSession : ISimSession
{
    private const int MaxStepsPerFrame = 12;
    public const int MinSpeed = 0;
    public const int MaxSpeed = 100;

    private readonly SimRuntime _sim;
    private readonly Queue<SimCommand> _commands = new();
    private readonly object _gate = new();

    private double _accumulator;
    private int _speed = 1;

    public SimSession(SimRuntime sim)
    {
        _sim = sim;

        Current = _sim.PublishSnapshot();
    }

    public TrackPictureSnapshot? Previous { get; private set; }

    public TrackPictureSnapshot? Current { get; private set; }

    public float Alpha { get; private set; }

    public int Speed => _speed;

    public int Tick => Current?.Tick ?? 0;

    public void Advance(double realDeltaSeconds)
    {
        if (realDeltaSeconds < 0)
        {
            realDeltaSeconds = 0;
        }

        _accumulator += realDeltaSeconds * _speed;

        int steps = 0;
        while (_accumulator >= 1.0 && steps < MaxStepsPerFrame)
        {
            StepOnce();
            _accumulator -= 1.0;
            steps++;
        }

        if (_accumulator >= 1.0)
        {
            _accumulator = 0;
        }

        Alpha = (float)Math.Clamp(_accumulator, 0.0, 1.0);
    }

    public void SetSpeed(int speed) => _speed = Math.Clamp(speed, MinSpeed, MaxSpeed);

    public void Enqueue(SimCommand command)
    {
        lock (_gate)
        {
            _commands.Enqueue(command);
        }
    }

    private void StepOnce()
    {
        DrainCommands();
        _sim.Step();
        Previous = Current;
        Current = _sim.PublishSnapshot();
    }

    private void DrainCommands()
    {
        while (true)
        {
            SimCommand command;
            lock (_gate)
            {
                if (_commands.Count == 0)
                {
                    return;
                }

                command = _commands.Dequeue();
            }

            switch (command)
            {
                case MoveCommand move:
                    _sim.IssueMoveOrder(move.UnitName, move.Destination);
                    break;
                case SpawnCommand spawn:
                    _sim.SpawnFromPrototype(spawn.PrototypeId, spawn.Position, spawn.Velocity);
                    break;
                case EngageCommand engage:
                    _sim.IssueEngagement(engage.UnitName, engage.TrackId, engage.Weapon, engage.Count);
                    break;
                case PostureCommand posture:
                    _sim.SetPosture(posture.UnitName, posture.Posture);
                    break;
                case LockCommand lockOrder:
                    _sim.SetLock(lockOrder.UnitName, lockOrder.TrackId);
                    break;
            }
        }
    }
}
