using Unity.Netcode;
using UnityEngine;

// What a character is told to do, from any source: local devices, a test bench or the network. Angles are
// absolute, so a lost or late update can't make the heading drift.
public struct CharacterInput : INetworkSerializable
{
    // Heading frame (x right, y forward), magnitude up to 1.
    public Vector2 Move;
    // Degrees about world up.
    public float HeadingYaw;
    // Camera pitch, degrees, + looking down. A reaching arm aims along it.
    public float LookPitch;
    // Held: that hand reaches along the aim and grips what it touches.
    public bool GrabLeft;
    public bool GrabRight;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Move);
        serializer.SerializeValue(ref HeadingYaw);
        serializer.SerializeValue(ref LookPitch);
        serializer.SerializeValue(ref GrabLeft);
        serializer.SerializeValue(ref GrabRight);
    }
}
