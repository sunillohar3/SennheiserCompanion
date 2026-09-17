using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using SennheiserMomentum4.Services.Logging;

namespace SennheiserMomentum4.Services.Device.Gaia;

public static class MomentumCommands
{
    public const ushort VendorId = 0x0495;
    public static readonly Guid RfcommServiceUuid = new("a2129ff3-081b-4c45-8afe-469d9c4842ec");

    public const ushort Battery = 0x0603;
    public const ushort BatteryResponse = 0x0703;

    public const ushort SetAudioMode = 0x0803;
    public const ushort SetAudioModeResponse = 0x0903;
    public const ushort GetSoundMode = 0x0804;
    public const ushort GetSoundModeResponse = 0x0904;

    public const ushort SetAncMode = 0x1a00;
    public const ushort SetAncModeResponse = 0x1b00;
    public const ushort GetAncModes = 0x1a01;
    public const ushort GetAncModesResponse = 0x1b01;

    public const ushort SetTransparencyLevel = 0x1a02;
    public const ushort SetTransparencyLevelResponse = 0x1b02;
    public const ushort GetTransparencyLevel = 0x1a03;
    public const ushort GetTransparencyLevelResponse = 0x1b03;

    public const ushort SetAncEnabled = 0x1a04;
    public const ushort SetAncEnabledResponse = 0x1b04;
    public const ushort GetAncEnabled = 0x1a05;
    public const ushort GetAncEnabledResponse = 0x1b05;

    public const ushort SetTransparentHearing = 0x1804;
    public const ushort SetTransparentHearingResponse = 0x1904;
    public const ushort GetTransparentHearing = 0x1805;
    public const ushort GetTransparentHearingResponse = 0x1905;

    public const ushort GetEqConfig = 0x1000;
    public const ushort GetEqConfigResponse = 0x1100;
    public const ushort SetEqBand = 0x1001;
    public const ushort SetEqBandResponse = 0x1101;
    public const ushort GetEqBand = 0x1002;
    public const ushort GetEqBandResponse = 0x1102;
    public const ushort SetBassBoost = 0x1008;
    public const ushort SetBassBoostResponse = 0x1108;
    public const ushort GetBassBoost = 0x1009;
    public const ushort GetBassBoostResponse = 0x1109;

    public const ushort GetProductId = 0x1200;
    public const ushort GetProductIdResponse = 0x1300;
    public const ushort GetFirmwareVersion = 0x1201;
    public const ushort GetFirmwareVersionResponse = 0x1301;
    public const ushort GetSerialNumber = 0x1202;
    public const ushort GetSerialNumberResponse = 0x1302;
    public const ushort GetHardwareVersion = 0x1203;
    public const ushort GetHardwareVersionResponse = 0x1303;
    public const ushort GetDeviceVariant = 0x1206;
    public const ushort GetDeviceVariantResponse = 0x1306;
}

public class GaiaPacket
{
    public ushort VendorId { get; }
    public ushort CommandId { get; }
    public byte[] Payload { get; }

    public GaiaPacket(ushort vendorId, ushort commandId, byte[]? payload = null)
    {
        VendorId = vendorId;
        CommandId = commandId;
        Payload = payload ?? Array.Empty<byte>();
    }

    public static GaiaPacket Parse(byte[] data)
    {
        if (data.Length < 4)
            throw new InvalidDataException($"GAIA packet too short: {data.Length} bytes.");

        ushort vendor = (ushort)((data[0] << 8) | data[1]);
        ushort command = (ushort)((data[2] << 8) | data[3]);
        byte[] payload = new byte[data.Length - 4];
        if (payload.Length > 0)
        {
            Array.Copy(data, 4, payload, 0, payload.Length);
        }

        return new GaiaPacket(vendor, command, payload);
    }

    public byte[] ToFrameBytes()
    {
        int payloadLen = Payload.Length;
        byte[] frame = new byte[8 + payloadLen];
        frame[0] = 0xFF;
        frame[1] = 0x03;
        frame[2] = (byte)((payloadLen >> 8) & 0xFF);
        frame[3] = (byte)(payloadLen & 0xFF);
        frame[4] = (byte)((VendorId >> 8) & 0xFF);
        frame[5] = (byte)(VendorId & 0xFF);
        frame[6] = (byte)((CommandId >> 8) & 0xFF);
        frame[7] = (byte)(CommandId & 0xFF);
        if (payloadLen > 0)
        {
            Array.Copy(Payload, 0, frame, 8, payloadLen);
        }
        return frame;
    }
}

public class GaiaSppDeframer
{
    private readonly List<byte> _buffer = new();

    public List<GaiaPacket> Ingest(byte[] incoming)
    {
        _buffer.AddRange(incoming);
        var packets = new List<GaiaPacket>();

        while (true)
        {
            // Find start of frame (0xFF, 0x03)
            int startIdx = -1;
            for (int i = 0; i < _buffer.Count - 1; i++)
            {
                if (_buffer[i] == 0xFF && _buffer[i + 1] == 0x03)
                {
                    startIdx = i;
                    break;
                }
            }

            if (startIdx < 0)
            {
                // No start marker found; keep only the last byte if it might be 0xFF
                if (_buffer.Count > 0 && _buffer[^1] == 0xFF)
                {
                    _buffer.RemoveRange(0, _buffer.Count - 1);
                }
                else
                {
                    _buffer.Clear();
                }
                break;
            }

            if (startIdx > 0)
            {
                _buffer.RemoveRange(0, startIdx);
            }

            if (_buffer.Count < 4)
                break;

            int payloadLen = (_buffer[2] << 8) | _buffer[3];
            int totalFrameLen = 8 + payloadLen;

            if (_buffer.Count < totalFrameLen)
                break; // Wait for full frame

            byte[] packetBytes = new byte[4 + payloadLen];
            _buffer.CopyTo(4, packetBytes, 0, packetBytes.Length);
            _buffer.RemoveRange(0, totalFrameLen);

            try
            {
                packets.Add(GaiaPacket.Parse(packetBytes));
            }
            catch
            {
                // Skip malformed packet
            }
        }

        return packets;
    }
}

public class GaiaSession : IAsyncDisposable
{
    private readonly IAppLogger _logger;
    private StreamSocket? _socket;
    private DataWriter? _writer;
    private DataReader? _reader;
    private CancellationTokenSource? _readCts;
    private Task? _readLoopTask;
    private readonly GaiaSppDeframer _deframer = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly object _pendingLock = new();
    private readonly List<PendingResponse> _pendingRequests = new();

    public bool IsConnected => _socket != null;

    private class PendingResponse
    {
        public HashSet<ushort> ExpectedCommands { get; }
        public TaskCompletionSource<GaiaPacket> Tcs { get; }

        public PendingResponse(HashSet<ushort> expectedCommands)
        {
            ExpectedCommands = expectedCommands;
            Tcs = new TaskCompletionSource<GaiaPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public GaiaSession(IAppLogger logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(RfcommDeviceService service, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await DisconnectInternalAsync();

            _logger.Bluetooth(LogLevel.Info, $"Connecting GaiaSession RFCOMM to {service.ConnectionHostName} / {service.ConnectionServiceName}...");
            var socket = new StreamSocket();
            await socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName).AsTask(cancellationToken);

            _socket = socket;
            _writer = new DataWriter(socket.OutputStream);
            _reader = new DataReader(socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };

            _readCts = new CancellationTokenSource();
            _readLoopTask = Task.Run(() => ReadLoopAsync(_readCts.Token));

            _logger.Bluetooth(LogLevel.Info, "GaiaSession RFCOMM control socket connected and read loop started.");
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Error, "Failed to connect GaiaSession RFCOMM socket", ex.Message);
            await DisconnectInternalAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        byte[] buffer = new byte[512];
        try
        {
            while (!ct.IsCancellationRequested && _reader != null)
            {
                uint loaded;
                try
                {
                    loaded = await _reader.LoadAsync(512).AsTask(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Bluetooth(LogLevel.Warn, "GaiaSession ReadLoop read error", ex.Message);
                    break;
                }

                if (loaded == 0)
                {
                    _logger.Bluetooth(LogLevel.Info, "GaiaSession connection closed by peer.");
                    break;
                }

                byte[] chunk = new byte[loaded];
                _reader.ReadBytes(chunk);

                List<GaiaPacket> packets;
                lock (_deframer)
                {
                    packets = _deframer.Ingest(chunk);
                }

                foreach (var packet in packets)
                {
                    if (packet.VendorId != MomentumCommands.VendorId)
                        continue;

                    DispatchPacket(packet);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Bluetooth(LogLevel.Warn, "GaiaSession ReadLoop terminated unexpectedly", ex.Message);
        }
        finally
        {
            FailPendingRequests(new IOException("GAIA RFCOMM connection was closed."));
        }
    }

    private void DispatchPacket(GaiaPacket packet)
    {
        lock (_pendingLock)
        {
            for (int i = _pendingRequests.Count - 1; i >= 0; i--)
            {
                var pending = _pendingRequests[i];
                if (pending.ExpectedCommands.Contains(packet.CommandId))
                {
                    _pendingRequests.RemoveAt(i);
                    pending.Tcs.TrySetResult(packet);
                    return;
                }
            }
        }

        _logger.Bluetooth(LogLevel.Debug, $"Unhandled GAIA packet: 0x{packet.CommandId:X4}, Payload: {BitConverter.ToString(packet.Payload)}");
    }

    private void FailPendingRequests(Exception ex)
    {
        lock (_pendingLock)
        {
            foreach (var pending in _pendingRequests)
            {
                pending.Tcs.TrySetException(ex);
            }
            _pendingRequests.Clear();
        }
    }

    public async Task<GaiaPacket> ExchangeAsync(
        ushort commandId,
        byte[]? payload = null,
        IEnumerable<ushort>? expecting = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_socket == null || _writer == null)
                throw new InvalidOperationException("GAIA RFCOMM control socket is not connected.");

            ushort defaultExpected = (ushort)(commandId | 0x0100);
            var expectedSet = expecting != null ? new HashSet<ushort>(expecting) : new HashSet<ushort> { defaultExpected };
            var pending = new PendingResponse(expectedSet);

            lock (_pendingLock)
            {
                _pendingRequests.Add(pending);
            }

            try
            {
                var packet = new GaiaPacket(MomentumCommands.VendorId, commandId, payload);
                byte[] frameBytes = packet.ToFrameBytes();
                _writer.WriteBytes(frameBytes);
                await _writer.StoreAsync().AsTask(cancellationToken);

                var timeoutDuration = timeout ?? TimeSpan.FromSeconds(3);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var delayTask = Task.Delay(timeoutDuration, linkedCts.Token);

                var completed = await Task.WhenAny(pending.Tcs.Task, delayTask);
                if (completed == pending.Tcs.Task)
                {
                    linkedCts.Cancel();
                    return await pending.Tcs.Task;
                }
                else
                {
                    throw new TimeoutException($"GAIA command 0x{commandId:X4} timed out waiting for responses [{string.Join(", ", expectedSet.Select(c => $"0x{c:X4}"))}].");
                }
            }
            finally
            {
                lock (_pendingLock)
                {
                    _pendingRequests.Remove(pending);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteControlAsync(
        ushort commandId,
        byte[] payload,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ushort successCode = (ushort)(commandId | 0x0100);
        ushort failureCode = (ushort)(successCode | 0x0080);

        var response = await ExchangeAsync(
            commandId,
            payload,
            new ushort[] { successCode, failureCode },
            timeout,
            cancellationToken);

        if (response.CommandId == failureCode)
        {
            byte status = response.Payload.Length > 0 ? response.Payload[0] : (byte)0xFF;
            throw new InvalidOperationException($"Headphones rejected command 0x{commandId:X4} (status code {status}).");
        }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await DisconnectInternalAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task DisconnectInternalAsync()
    {
        _readCts?.Cancel();
        _readCts?.Dispose();
        _readCts = null;

        FailPendingRequests(new OperationCanceledException("Session disconnected."));

        _writer?.Dispose();
        _writer = null;

        _reader?.Dispose();
        _reader = null;

        _socket?.Dispose();
        _socket = null;

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _gate.Dispose();
    }
}
