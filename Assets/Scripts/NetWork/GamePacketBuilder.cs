using Google.FlatBuffers;
using game;

public static class GamePacketBuilder
{
    public static byte[] BuildLogin(string userId, string token)
    {
        var fbb = new FlatBufferBuilder(128);

        var uid = fbb.CreateString(userId);
        var tok = fbb.CreateString(token);

        Login.StartLogin(fbb);
        Login.AddUserId(fbb, uid);
        Login.AddToken(fbb, tok);
        var loginOffset = Login.EndLogin(fbb);

        Envelope.StartEnvelope(fbb);
        Envelope.AddPktType(fbb, Packet.Login);
        Envelope.AddPkt(fbb, loginOffset.Value);
        var envOffset = Envelope.EndEnvelope(fbb);

        fbb.Finish(envOffset.Value);
        return fbb.SizedByteArray();
    }

    public static byte[] BuildEnterField(uint fieldId)
    {
        var fbb = new FlatBufferBuilder(64);

        var enterOffset = EnterField.CreateEnterField(fbb, fieldId);

        Envelope.StartEnvelope(fbb);
        Envelope.AddPktType(fbb, Packet.EnterField);
        Envelope.AddPkt(fbb, enterOffset.Value);
        var envOffset = Envelope.EndEnvelope(fbb);

        fbb.Finish(envOffset.Value);
        return fbb.SizedByteArray();
    }
}
