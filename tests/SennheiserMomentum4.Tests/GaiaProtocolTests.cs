using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SennheiserMomentum4.Services.Device.Gaia;

namespace SennheiserMomentum4.Tests;

public class GaiaProtocolTests
{
    [Fact]
    public void GaiaPacket_ToFrameBytes_EncodesCorrectSppHeaderAndLength()
    {
        // EQ Config Request: Vendor 0x0495, Command 0x1000, 0 payload bytes
        var packet = new GaiaPacket(0x0495, 0x1000, Array.Empty<byte>());
        var frame = packet.ToFrameBytes();

        Assert.Equal(8, frame.Length);
        Assert.Equal(0xFF, frame[0]);
        Assert.Equal(0x03, frame[1]);
        Assert.Equal(0x00, frame[2]); // Length high
        Assert.Equal(0x00, frame[3]); // Length low
        Assert.Equal(0x04, frame[4]); // Vendor high
        Assert.Equal(0x95, frame[5]); // Vendor low
        Assert.Equal(0x10, frame[6]); // Cmd high
        Assert.Equal(0x00, frame[7]); // Cmd low
    }

    [Fact]
    public void GaiaPacket_ToFrameBytes_EncodesPayloadCorrectly()
    {
        // SetEqBand: Vendor 0x0495, Cmd 0x1001, payload: [band=1, gain=25]
        byte[] payload = new byte[] { 0x01, 0x19 };
        var packet = new GaiaPacket(0x0495, 0x1001, payload);
        var frame = packet.ToFrameBytes();

        Assert.Equal(10, frame.Length);
        Assert.Equal(0xFF, frame[0]);
        Assert.Equal(0x03, frame[1]);
        Assert.Equal(0x00, frame[2]);
        Assert.Equal(0x02, frame[3]); // Payload length = 2
        Assert.Equal(0x04, frame[4]);
        Assert.Equal(0x95, frame[5]);
        Assert.Equal(0x10, frame[6]);
        Assert.Equal(0x01, frame[7]);
        Assert.Equal(0x01, frame[8]);
        Assert.Equal(0x19, frame[9]);
    }

    [Fact]
    public void GaiaSppDeframer_Ingest_ExtractsCompletePacketFromStream()
    {
        var deframer = new GaiaSppDeframer();
        // Frame: FF 03 00 05 04 95 11 00 05 C4 3C 00 00 (EqConfig response from live headset)
        byte[] rawStream = new byte[] { 0xFF, 0x03, 0x00, 0x05, 0x04, 0x95, 0x11, 0x00, 0x05, 0xC4, 0x3C, 0x00, 0x00 };

        var packets = deframer.Ingest(rawStream);

        Assert.Single(packets);
        var p = packets[0];
        Assert.Equal(0x0495, p.VendorId);
        Assert.Equal(0x1100, p.CommandId);
        Assert.Equal(5, p.Payload.Length);
        Assert.Equal(5, p.Payload[0]);    // 5 bands
        Assert.Equal(0xC4, p.Payload[1]); // -60 tenths (-6.0 dB)
        Assert.Equal(0x3C, p.Payload[2]); // +60 tenths (+6.0 dB)
    }

    [Fact]
    public void GaiaSppDeframer_Ingest_HandlesFragmentedIncomingStream()
    {
        var deframer = new GaiaSppDeframer();
        byte[] part1 = new byte[] { 0xFF, 0x03, 0x00, 0x01 };
        byte[] part2 = new byte[] { 0x04, 0x95, 0x1B, 0x05, 0x01 }; // ANC enabled = 1

        var packets1 = deframer.Ingest(part1);
        Assert.Empty(packets1); // Not enough bytes yet

        var packets2 = deframer.Ingest(part2);
        Assert.Single(packets2);
        Assert.Equal(0x0495, packets2[0].VendorId);
        Assert.Equal(0x1B05, packets2[0].CommandId);
        Assert.Equal(1, packets2[0].Payload[0]);
    }

    [Fact]
    public void GaiaSppDeframer_Ingest_RecoversFromGarbageBytesBeforeFrame()
    {
        var deframer = new GaiaSppDeframer();
        byte[] streamWithGarbage = new byte[] { 0x12, 0x34, 0x56, 0xFF, 0x03, 0x00, 0x01, 0x04, 0x95, 0x1B, 0x03, 0x4B };

        var packets = deframer.Ingest(streamWithGarbage);

        Assert.Single(packets);
        Assert.Equal(0x1B03, packets[0].CommandId);
        Assert.Equal(0x4B, packets[0].Payload[0]); // Transparency 75%
    }
}
