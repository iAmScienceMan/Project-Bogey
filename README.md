# Project Bogey

Very, very small (at least I hope for now) fog of war tactical war simulator game written in C# / .NET.

## The main idea

The actual truth and the believed picture are kept completely separate, and that is enforced by the assembly boundaries, not by human review or something else.

## Cloning

The engine lives in a separate repo, [LatticeToolbox](https://github.com/iAmScienceMan/LatticeToolbox),
and is pulled in here as a git submodule (the way Space Station 14 uses RobustToolbox). Clone with
submodules:

```bash
git clone --recurse-submodules git@github.com:iAmScienceMan/Project-Bogey.git
```

Already cloned without `--recurse-submodules`? Fetch the engine with:

```bash
git submodule update --init --recursive
```

## Build and run

You need the .NET 10 SDK.

```bash
dotnet build
dotnet test
dotnet run --project Content.Host -- --render --debug
```
**NOTE:** View the flags by running the thing with a non-existent flag.

In the future I think of moving `--debug` option to different dotnet configurations, but for now it is like this.
