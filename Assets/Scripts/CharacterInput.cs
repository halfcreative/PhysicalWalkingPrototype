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
    // Camera pitch, degrees, + looking down.
    public float LookPitch;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Move);
        serializer.SerializeValue(ref HeadingYaw);
        serializer.SerializeValue(ref LookPitch);
    }
}
