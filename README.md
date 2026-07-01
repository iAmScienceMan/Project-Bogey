# Project Bogey

Very, very small (at least I hope for now) fog of war tactical war simulator game written in C# / .NET.

## The main idea

The actual truth and the believed picture are kept completely separate, and that is enforced by the assembly boundaries, not by human review or something else.

## Build and run

You need the .NET 10 SDK.

```bash
dotnet build
dotnet test
dotnet run --project src/Bogey.Host -- --render --debug
```
**NOTE:** View the flags by running the thing with a non-existent flag.

In the future I think of moving `--debug` option to different dotnet configurations, but for now it is like this.
