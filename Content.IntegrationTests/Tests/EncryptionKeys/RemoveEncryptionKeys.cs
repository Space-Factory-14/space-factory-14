using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Radio.Components;
using Content.Shared.Wires;

namespace Content.IntegrationTests.Tests.EncryptionKeys;

public sealed class RemoveEncryptionKeys : InteractionTest
{
    [Test]
    public async Task HeadsetKeys()
    {
        await SpawnTarget("ClothingHeadsetGrey");
        var comp = Comp<EncryptionKeyHolderComponent>();

        Assert.Multiple(() =>
        {
            Assert.That(comp.KeyContainer.ContainedEntities, Has.Count.EqualTo(1));
            Assert.That(comp.DefaultChannel, Is.EqualTo("Engineering")); // SpaceFactory - Common to Engineering
            Assert.That(comp.Channels, Has.Count.EqualTo(1));
            Assert.That(comp.Channels.First(), Is.EqualTo("Engineering")); // SpaceFactory - Common to Engineering
        });

        // Remove the key
        await Interact(Screw);
        Assert.Multiple(() =>
        {
            Assert.That(comp.KeyContainer.ContainedEntities, Has.Count.EqualTo(0));
            Assert.That(comp.DefaultChannel, Is.Null);
            Assert.That(comp.Channels, Has.Count.EqualTo(0));
        });

        // Check that the key was ejected and not just deleted or something.
        await AssertEntityLookup(("EncryptionKeyEngineering", 1)); // SpaceFactory - Common to Engineering

        // Re-insert a key.
        await Interact("EncryptionKeyEngineering"); // SpaceFactory - CentCom to Engineering
        Assert.Multiple(() =>
        {
            Assert.That(comp.KeyContainer.ContainedEntities, Has.Count.EqualTo(1));
            Assert.That(comp.DefaultChannel, Is.EqualTo("Engineering")); // SpaceFactory - CentCom to Engineering
            Assert.That(comp.Channels, Has.Count.EqualTo(1));
            Assert.That(comp.Channels.First(), Is.EqualTo("Engineering")); // SpaceFactory - CentCom to Engineering
        });
    }

    [Test]
    public async Task CommsServerKeys()
    {
        await SpawnTarget("TelecomServerFilled");
        var comp = Comp<EncryptionKeyHolderComponent>();
        var panel = Comp<WiresPanelComponent>();

        Assert.Multiple(() =>
        {
            Assert.That(comp.KeyContainer.ContainedEntities, Has.Count.GreaterThan(0));
            Assert.That(comp.Channels, Has.Count.GreaterThan(0));
            Assert.That(panel.Open, Is.False);
        });

        // cannot remove keys without opening panel
        await Interact(Pry);
        Assert.Multiple(() =>
        {
            Assert.That(comp.KeyContainer.ContainedEntities, Has.Count.GreaterThan(0));
            Assert.That(comp.Channels, Has.Count.GreaterThan(0));
            Assert.That(panel.Open, Is.False);
        });

        // Open panel
        await Interact(Screw);
        Assert.Multiple(() =>
        {
            Assert.That(panel.Open, Is.True);

            // Keys are still here
            Assert.That(comp.KeyContainer.ContainedEntities, Has.Count.GreaterThan(0));
            Assert.That(comp.Channels, Has.Count.GreaterThan(0));
        });

        // Now remove the keys
        await Interact(Pry);
        Assert.Multiple(() =>
        {
            Assert.That(comp.KeyContainer.ContainedEntities, Has.Count.EqualTo(0));
            Assert.That(comp.Channels, Has.Count.EqualTo(0));
        });

        // Reinsert a key
        await Interact("EncryptionKeyEngineering"); // SpaceFactory - CentCom to Engineering
        Assert.Multiple(() =>
        {
            Assert.That(comp.KeyContainer.ContainedEntities, Has.Count.EqualTo(1));
            Assert.That(comp.DefaultChannel, Is.EqualTo("Engineering")); // SpaceFactory - CentCom to Engineering
            Assert.That(comp.Channels, Has.Count.EqualTo(1));
            Assert.That(comp.Channels.First(), Is.EqualTo("Engineering")); // SpaceFactory - CentCom to Engineering
        });

        // Remove it again
        await Interact(Pry);
        Assert.Multiple(() =>
        {
            Assert.That(comp.KeyContainer.ContainedEntities, Has.Count.EqualTo(0));
            Assert.That(comp.Channels, Has.Count.EqualTo(0));
        });

        // Prying again will start deconstructing the machine.
        AssertPrototype("TelecomServerFilled");
        await Interact(Pry);
        AssertPrototype("MachineFrame");
    }
}
