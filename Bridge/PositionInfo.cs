
using Meshtastic.Protobufs;

public class PositionInfo
{
    public Position Position { get; set; }
    public uint From { get; set; }
}
public class KnownNode {
    public uint From { get; set; }
    public string LongName { get; set; }    
}


public class Node
{
    public NodeInfo NodeInfo{ get; set; }

    public uint From { get; set; }
    public ServiceEnvelope ServiceEnvelope { get; internal set; }
}