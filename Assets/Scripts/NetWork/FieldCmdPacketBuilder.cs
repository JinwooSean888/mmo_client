using Google.FlatBuffers;
using field;

public static class FieldCmdPacketBuilder
{
    public static byte[] BuildEnter(ulong playerId, float x, float y)
    {
        var fbb = new FlatBufferBuilder(64);

        var pos = Vec2.CreateVec2(fbb, x, y);

        FieldCmd.StartFieldCmd(fbb);
        FieldCmd.AddType(fbb, FieldCmdType.Enter);
        FieldCmd.AddEntityType(fbb, EntityType.Player); // ★ 타입 명시
        FieldCmd.AddEntityId(fbb, playerId);            // ★ entityId 사용
        FieldCmd.AddPos(fbb, pos);
        // dir는 클라→서버용이라 이쪽에선 안 채워도 됨 (기본값)
        var cmd = FieldCmd.EndFieldCmd(fbb);

        fbb.Finish(cmd.Value);
        return fbb.SizedByteArray();
    }

    public static byte[] BuildLeave(ulong playerId)
    {
        var fbb = new FlatBufferBuilder(32);

        // 지금 fbs에서 pos가 필수라면 dummy라도 넣어야 함
        var pos = Vec2.CreateVec2(fbb, 0, 0);

        FieldCmd.StartFieldCmd(fbb);
        FieldCmd.AddType(fbb, FieldCmdType.Leave);
        FieldCmd.AddEntityType(fbb, EntityType.Player); // ★ Player
        FieldCmd.AddEntityId(fbb, playerId);            // ★ entityId
        FieldCmd.AddPos(fbb, pos);
        var cmd = FieldCmd.EndFieldCmd(fbb);

        fbb.Finish(cmd.Value);
        return fbb.SizedByteArray();
    }

    public static byte[] BuildMoveInput(ulong playerId, float dx, float dy)
    {
        var fbb = new FlatBufferBuilder(128);

        // 클라→서버 MoveInput에서는 pos는 안 쓰지만, fbs에서 필수면 dummy
        var pos = Vec2.CreateVec2(fbb, 0, 0);
        var dir = Vec2.CreateVec2(fbb, dx, dy);

        var cmd = FieldCmd.CreateFieldCmd(
            fbb,
            FieldCmdType.Move,        // type
            EntityType.Player,        // ★ entityType
            playerId,                 // ★ entityId
            pos,                      // pos (서버는 무시해도 됨)
            dir                       // dir (입력 방향)
        );
        fbb.Finish(cmd.Value);

        return fbb.SizedByteArray();
    }
}
