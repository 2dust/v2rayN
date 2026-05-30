namespace ServiceLib.UdpTest.Tester;

public class McBeService : IUdpTest
{
    private const int McBeDefaultPort = 19132;
    private const string McBeDefaultServer = "pms.mc-complex.com";

    // 0x01 | client alive time in ms (unsigned long long) | magic | client GUID
    private static readonly byte[] McBeQueryPacket =
    [
        // 0x01
        0x01,
        // Client alive time (1000 ms)
        0x27, 0xC4, 0x15, 0x00, 0x00, 0x00, 0x00, 0x00,
        // Magic
        0x00, 0xFF, 0xFF, 0x00, 0xFE, 0xFE, 0xFE, 0xFE,
        0xFD, 0xFD, 0xFD, 0xFD, 0x12, 0x34, 0x56, 0x78,
        // Client GUID (random 16 bytes)
        0x66, 0x0E, 0xAB, 0xBC, 0x61, 0x0D, 0x1F, 0x4E,
        0xA4, 0x40, 0x8C, 0x65, 0xC1, 0xBE, 0xF5, 0x4B
    ];

    private static readonly byte[] McBeMagicBytes =
    [
        0x00, 0xFF, 0xFF, 0x00, 0xFE, 0xFE, 0xFE, 0xFE,
        0xFD, 0xFD, 0xFD, 0xFD, 0x12, 0x34, 0x56, 0x78
    ];

    private static readonly List<string> ValidGameModes =
    [
        "Survival",
        "Creative",
        "Adventure",
        "Spectator"
    ];

    public byte[] BuildUdpRequestPacket()
    {
        return (byte[])McBeQueryPacket.Clone();
    }

    public bool VerifyAndExtractUdpResponse(byte[] mcbeResponseBytes)
    {
        // 0x1c | client alive time in ms (recorded from previous ping) |
        // server GUID | Magic | string length | Edition
        //
        // Edition Example:
        //
        // MCPE;Dedicated Server;527;1.19.1;0;10;13253860892328930865;Bedrock level;Survival;1;19132;19133;
        if (mcbeResponseBytes.Length < 48)
        {
            return false;
        }
        if (mcbeResponseBytes[0] != 0x1C)
        {
            return false; // Invalid packet type
        }
        var pongMagic = mcbeResponseBytes.Skip(17).Take(16).ToArray();
        if (!pongMagic.SequenceEqual(McBeMagicBytes))
        {
            return false; // Magic bytes do not match
        }
        var stringLength = (ushort)((mcbeResponseBytes[33] << 8) | mcbeResponseBytes[34]);
        var stringData = Encoding.UTF8.GetString(mcbeResponseBytes.Skip(35).Take(stringLength).ToArray());
        var stringParts = stringData.Split(';');
        // check Game Mode str
        var gameMode = stringParts.Length > 8 ? stringParts[8] : "";
        if (!ValidGameModes.Contains(gameMode))
        {
            return false; // Invalid game mode
        }
        return true;
    }

    public ushort GetDefaultTargetPort()
    {
        return McBeDefaultPort;
    }

    public string GetDefaultTargetHost()
    {
        return McBeDefaultServer;
    }
}
