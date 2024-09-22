
using Meshtastic.Protobufs;

public class PositionInfo
{
    // For Bob: Proto buf version is sealed so can't inherit
    public Position Position { get; set; }
    public uint From { get; set; }
}

