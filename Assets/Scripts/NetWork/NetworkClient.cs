using System;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;

public class NetworkClient
{
    private TcpClient _tcp;
    private NetworkStream _stream;
    private Thread _recvThread;
    private volatile bool _running;

    // 완성된 패킷 바디(FlatBuffers bytes)를 넘겨주는 큐
    public ConcurrentQueue<byte[]> RecvQueue { get; } = new ConcurrentQueue<byte[]>();

    public bool IsConnected => _tcp != null && _tcp.Connected;

    public void Connect(string host, int port)
    {
        if (IsConnected)
            return;

        try
        {
            _tcp = new TcpClient();
            _tcp.Connect(host, port);
            _stream = _tcp.GetStream();

            _running = true;
            _recvThread = new Thread(RecvLoop);
            _recvThread.IsBackground = true;
            _recvThread.Start();

            Debug.Log($"[NET] Connected to {host}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NET] Connect failed: {e}");
            Close();
        }
    }

    public void Close()
    {
        _running = false;

        try { _stream?.Close(); } catch { }
        try { _tcp?.Close(); } catch { }

        _stream = null;
        _tcp = null;
    }

    // [len(4byte LE)] + [body] 형태로 전송
    public void SendPayload(byte[] payload)
    {
        if (!IsConnected || _stream == null)
            return;

        try
        {
            byte[] lenBuf = BitConverter.GetBytes(payload.Length); // little endian
            _stream.Write(lenBuf, 0, 4);
            _stream.Write(payload, 0, payload.Length);
        }
        catch (Exception e)
        {
            Debug.LogError($"[NET] Send failed: {e}");
            Close();
        }
    }

    private void RecvLoop()
    {
        try
        {
            while (_running && _stream != null)
            {
                // 1) length 4바이트
                byte[] lenBuf = new byte[4];
                if (!ReadExact(lenBuf, 4))
                    break;

                int bodyLen = BitConverter.ToInt32(lenBuf, 0);
                if (bodyLen <= 0 || bodyLen > 1024 * 1024)
                {
                    Debug.LogError($"[NET] invalid bodyLen={bodyLen}");
                    break;
                }

                // 2) body
                byte[] body = new byte[bodyLen];
                if (!ReadExact(body, bodyLen))
                    break;

                RecvQueue.Enqueue(body);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NET] RecvLoop error: {e}");
        }

        _running = false;
        Debug.Log("[NET] RecvLoop end");
    }

    private bool ReadExact(byte[] buf, int size)
    {
        int offset = 0;
        while (offset < size)
        {
            int n = _stream.Read(buf, offset, size - offset);
            if (n <= 0)
                return false;

            offset += n;
        }
        return true;
    }
}
