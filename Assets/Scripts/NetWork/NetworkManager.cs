using UnityEngine;
using game;
using field;
using Google.FlatBuffers;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [Header("Server")]
    public string host = "127.0.0.1";
    public int port = 9000;

    [Header("My Info")]
    public string userId = "test_user";
    public string token = "dummy_token";
    public uint fieldId = 1000;   // 입장할 필드 ID
    public ulong myPlayerId = 1;   // 서버에서 할당해 줄 수도 있음 (이거 안씀)
    public bool _inField = false;

    private NetworkClient _client = new NetworkClient();
    

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        while (_client != null && _client.RecvQueue.TryDequeue(out var body))
        {
            if (_inField)
                HandleFieldCmd(body);
            else
                HandleEnvelope(body);
        }
    }

    private void OnDestroy()
    {
        _client?.Close();
    }

    public void Connect()
    {
        Debug.Log("[NET] Connect()");
        _client.Connect(host, port);

        // 간단히: 접속 직후 바로 Login, EnterField 전송
        SendLogin();
        
    }

    public void SendLogin()
    {
        var payload = GamePacketBuilder.BuildLogin(userId, token);
        _client.SendPayload(payload);
        Debug.Log("[NET] Login sent");
    }

    public void SendEnterField()
    {
        var payload = GamePacketBuilder.BuildEnterField(fieldId);
        _client.SendPayload(payload);
        Debug.Log("[NET] EnterField sent");
    }

    // 플레이어 이동 시 호출
    public void SendFieldMove(Vector2 pos2D)
    {
        if (!_inField) return;

        var payload = FieldCmdPacketBuilder.BuildMoveInput(myPlayerId, pos2D.x, pos2D.y);
        _client.SendPayload(payload);
    }

    private void HandleEnvelope(byte[] body)
    {
        var bb = new ByteBuffer(body);
        var env = Envelope.GetRootAsEnvelope(bb);

        //Debug.Log("Recv Envelope type=" + env.PktType);

        switch (env.PktType)
        {
            case Packet.LoginAck:
                {
                    var ack = env.Pkt<LoginAck>().Value;
                    Debug.Log($"[RECV] LoginAck ok={ack.Ok}, user={ack.UserId}");

                    if (ack.Ok)
                    {
                        myPlayerId = ack.PlayerId;
                        fieldId = (uint)ack.DefaultFieldId;

                        Debug.Log("SendEnterField()");
                        SendEnterField();
                    }
                    break;
                }

            case Packet.EnterFieldAck:      // ★ Ack 타입
                {
                    var enterAck = env.Pkt<EnterFieldAck>().Value;
                    AoiWorld.MyPlayerId = enterAck.PlayerId;
                    AoiWorld.ForceAllMonstersOff("enterfield");
                    Debug.Log($"[EnterFieldAck] MyPlayerId = {AoiWorld.MyPlayerId}");

                    _inField = true;            // ★ 여기서 ON
                    break;
                }
        }
    }
    public void SendSkillAttack(ulong targetId)
    {
        // 필드에 들어가 있지 않으면 무시
        if (!_inField) return;
        if (_client == null) return;

        // 1) FlatBuffer 빌더 생성
        var fbb = new FlatBufferBuilder(64);

        // 2) SkillCmd 본문 생성
        var skillOffset = SkillCmd.CreateSkillCmd(
            fbb,
            SkillType.NormalAttack,   // 지금은 노멀 공격만
            targetId                  // 타겟 몬스터/플레이어 ID
        );

        // 3) Envelope 로 포장
        //    Packet enum에 SkillCmd 가 추가되어 있어야 함
        var envOffset = Envelope.CreateEnvelope(
            fbb,
            Packet.SkillCmd,          // 실제 이름은 Generated/Packet.cs 에서 확인
            skillOffset.Value
        );

        // 4) root_type Envelope 기준으로 Finish
        fbb.Finish(envOffset.Value);

        // 5) 서버로 전송 (Frame 인코딩은 _client 내부에서 처리한다고 가정)
        _client.SendPayload(fbb.SizedByteArray());
    }


    private void HandleFieldCmd(byte[] body)
    {
        var bb = new ByteBuffer(body);
        var cmd = FieldCmd.GetRootAsFieldCmd(bb);

      //  Debug.Log($"[CL] Recv FieldCmd type={cmd.Type} pid={cmd.EntityId} pos=({cmd.Pos.Value.X}, {cmd.Pos.Value.Y})");

        AoiWorld.ApplyFieldCmd(cmd);
    }

}
