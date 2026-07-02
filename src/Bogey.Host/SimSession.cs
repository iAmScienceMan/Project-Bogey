using System;
using System.Collections.Generic;
using Bogey.Renderer.RealTime;
using Bogey.Shared.Commands;
using Bogey.Shared.Tracks;
using Bogey.Sim;

namespace Bogey.Host;

public sealed class SimSession : ISimSession
{
    private const int MaxStepsPerFrame = 12;

    private readonly SimRuntime _sim;
    private readonly double _normalTicksPerSecond;
    private readonly double _fastTicksPerSecond;
    private readonly Queue<SimCommand> _commands = new();
    private readonly object _gate = new();

    private double _accumulator;
    private SimSpeed _speed = SimSpeed.Normal;

    public SimSession(SimRuntime sim, double normalTicksPerSecond = 1.0, double fastTicksPerSecond = 10.0)
    {
        _sim = sim;
        _normalTicksPerSecond = normalTicksPerSecond > 0 ? normalTicksPerSecond : 1.0;
        _fastTicksPerSecond = fastTicksPerSecond > 0 ? fastTicksPerSecond : 10.0;

        Current = _sim.PublishSnapshot();
    }

    public TrackPictureSnapshot? Previous { get; private set; }

    public TrackPictureSnapshot? Current { get; private set; }

    public float Alpha { get; private set; }

    public SimSpeed Speed => _speed;

    public int Tick => Current?.Tick ?? 0;

    public void Advance(double realDeltaSeconds)
    {
        if (realDeltaSeconds < 0)
        {
            realDeltaSeconds = 0;
        }

        _accumulator += realDeltaSeconds * TicksPerSecond(_speed);

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

    public void SetSpeed(SimSpeed speed) => _speed = speed;

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

            if (command is MoveCommand move)
            {
                _sim.IssueMoveOrder(move.UnitName, move.Destination);
            }
        }
    }

    private double TicksPerSecond(SimSpeed speed) => speed switch
    {
        SimSpeed.Paused => 0.0,
        SimSpeed.Normal => _normalTicksPerSecond,
        SimSpeed.Fast => _fastTicksPerSecond,
        _ => _normalTicksPerSecond,
    };
}
