using System.IO;
using Content.Shared.Configuration;
using Lattice.Shared.Configuration;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class ConfigurationManagerTests
{
    private enum Mode
    {
        Off,
        On,
        Auto,
    }

    private static readonly CVarDef<int> IntVar = CVarDef.Create("test.int", 5);
    private static readonly CVarDef<float> FloatVar = CVarDef.Create("test.float", 1.5f);
    private static readonly CVarDef<bool> BoolVar = CVarDef.Create("test.bool", false);
    private static readonly CVarDef<string> StringVar = CVarDef.Create("test.string", "hi");
    private static readonly CVarDef<Mode> ModeVar = CVarDef.Create("test.mode", Mode.Off);
    private static readonly CVarDef<int> ArchivedVar = CVarDef.Create("test.archived", 1, CVarFlags.Archive);

    private static ConfigurationManager Make()
    {
        ConfigurationManager cfg = new();
        cfg.Register(IntVar);
        cfg.Register(FloatVar);
        cfg.Register(BoolVar);
        cfg.Register(StringVar);
        cfg.Register(ModeVar);
        cfg.Register(ArchivedVar);
        return cfg;
    }

    [Test]
    public void GetCVar_ReturnsDefault_WhenUnset()
    {
        ConfigurationManager cfg = Make();
        Assert.That(cfg.GetCVar(IntVar), Is.EqualTo(5));
        Assert.That(cfg.GetCVar(FloatVar), Is.EqualTo(1.5f));
        Assert.That(cfg.GetCVar(StringVar), Is.EqualTo("hi"));
    }

    [Test]
    public void SetCVar_RoundTrips()
    {
        ConfigurationManager cfg = Make();
        cfg.SetCVar(IntVar, 42);
        Assert.That(cfg.GetCVar(IntVar), Is.EqualTo(42));
    }

    [Test]
    public void TrySetCVar_ParsesEachSupportedType()
    {
        ConfigurationManager cfg = Make();

        Assert.Multiple(() =>
        {
            Assert.That(cfg.TrySetCVar("test.int", "17", out _), Is.True);
            Assert.That(cfg.TrySetCVar("test.float", "2.25", out _), Is.True);
            Assert.That(cfg.TrySetCVar("test.bool", "on", out _), Is.True);
            Assert.That(cfg.TrySetCVar("test.string", "callsign one", out _), Is.True);
            Assert.That(cfg.TrySetCVar("test.mode", "auto", out _), Is.True);
        });

        Assert.Multiple(() =>
        {
            Assert.That(cfg.GetCVar(IntVar), Is.EqualTo(17));
            Assert.That(cfg.GetCVar(FloatVar), Is.EqualTo(2.25f));
            Assert.That(cfg.GetCVar(BoolVar), Is.True);
            Assert.That(cfg.GetCVar(StringVar), Is.EqualTo("callsign one"));
            Assert.That(cfg.GetCVar(ModeVar), Is.EqualTo(Mode.Auto));
        });
    }

    [Test]
    public void TrySetCVar_BadInput_ReturnsError_AndLeavesValue()
    {
        ConfigurationManager cfg = Make();

        Assert.Multiple(() =>
        {
            Assert.That(cfg.TrySetCVar("test.int", "not-a-number", out string? error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(cfg.GetCVar(IntVar), Is.EqualTo(5));
        });
    }

    [Test]
    public void TrySetCVar_UnknownName_ReturnsError()
    {
        ConfigurationManager cfg = Make();
        Assert.That(cfg.TrySetCVar("does.not.exist", "1", out string? error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void GetCVarString_FormatsInvariant()
    {
        ConfigurationManager cfg = Make();
        cfg.SetCVar(FloatVar, 3.5f);
        Assert.That(cfg.GetCVarString("test.float"), Is.EqualTo("3.5"));
    }

    [Test]
    public void OnValueChanged_FiresOnSet_AndImmediately()
    {
        ConfigurationManager cfg = Make();

        int immediate = -1;
        cfg.OnValueChanged(IntVar, v => immediate = v, invokeImmediately: true);
        Assert.That(immediate, Is.EqualTo(5));

        cfg.SetCVar(IntVar, 9);
        Assert.That(immediate, Is.EqualTo(9));
    }

    [Test]
    public void ResetToDefault_RestoresDefault()
    {
        ConfigurationManager cfg = Make();
        cfg.SetCVar(IntVar, 100);
        cfg.ResetToDefault(IntVar);
        Assert.That(cfg.GetCVar(IntVar), Is.EqualTo(5));
    }

    [Test]
    public void Persistence_RoundTripsArchivedCVars_ButNotUnflagged()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            ConfigurationManager saved = Make();
            saved.EnablePersistence(path);
            saved.SetCVar(ArchivedVar, 99);
            saved.SetCVar(IntVar, 42);

            ConfigurationManager loaded = Make();
            loaded.LoadArchive(path);

            Assert.Multiple(() =>
            {
                Assert.That(loaded.GetCVar(ArchivedVar), Is.EqualTo(99));
                Assert.That(loaded.GetCVar(IntVar), Is.EqualTo(5));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void EnablePersistence_DoesNotResaveOnPlainLoad()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            ConfigurationManager cfg = Make();
            cfg.LoadArchive(path);
            Assert.That(File.Exists(path), Is.False);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void RegisterCVars_ReflectsHolderType()
    {
        ConfigurationManager cfg = new();
        cfg.RegisterCVars(typeof(CVars));
        cfg.RegisterCVars(typeof(CCVars));

        Assert.Multiple(() =>
        {
            Assert.That(cfg.IsRegistered("ui.scale"), Is.True);
            Assert.That(cfg.IsRegistered("game.seed"), Is.True);
            Assert.That(cfg.GetCVar(CVars.UiScale), Is.EqualTo(1f));
            Assert.That(cfg.GetCVar(CCVars.GameSeed), Is.EqualTo(1337));
        });
    }
}
