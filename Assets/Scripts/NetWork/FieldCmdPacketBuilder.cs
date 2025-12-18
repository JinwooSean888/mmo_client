using Google.FlatBuffers;
using field;

public static class FieldCmdPacketBuilder
{
    public static byte[] BuildEnter(ulong playerId, float x, float y)
    {
        var fbb = new FlatBufferBuilder(64);

        var pos = Vec2.CreateVec2(fbb, x, y);

        var cmd = FieldCmd.CreateFieldCmd(
            fbb,
            FieldCmdType.Enter,
            EntityType.Player,
            playerId,
            pos,
            default(Offset<Vec2>),
            default(StringOffset)
        );

        var env = Envelope.CreateEnvelope(
            fbb,
            Packet.FieldCmd,
            cmd.Value
        );

        fbb.Finish(env.Value);
        return fbb.SizedByteArray();
    }

    public static byte[] BuildLeave(ulong playerId)
    {
        var fbb = new FlatBufferBuilder(32);

        var pos = Vec2.CreateVec2(fbb, 0, 0);

        var cmd = FieldCmd.CreateFieldCmd(
            fbb,
            FieldCmdType.Leave,
            EntityType.Player,
            playerId,
            pos,
            default(Offset<Vec2>),
            default(StringOffset)
        );

        var env = Envelope.CreateEnvelope(
            fbb,
            Packet.FieldCmd,
            cmd.Value
        );

        fbb.Finish(env.Value);
        return fbb.SizedByteArray();
    }

    public static byte[] BuildMoveInput(ulong playerId, float dx, float dy)
    {
        var fbb = new FlatBufferBuilder(128);

        var pos = Vec2.CreateVec2(fbb, 0, 0);
        var dir = Vec2.CreateVec2(fbb, dx, dy);

        var cmd = FieldCmd.CreateFieldCmd(
            fbb,
            FieldCmdType.Move,
            EntityType.Player,
            playerId,
            pos,
            dir,
            default(StringOffset)
        );

        var env = Envelope.CreateEnvelope(
            fbb,
            Packet.FieldCmd,
            cmd.Value
        );

        fbb.Finish(env.Value);
        return fbb.SizedByteArray();
    }
}
