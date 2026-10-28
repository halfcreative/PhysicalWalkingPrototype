using Unity.Netcode;
using UnityEngine;

// Every body of one character at one network tick, so a client can draw them all from the same physics
// step. The first body's position is sent in full; the rest are half-precision offsets from it (a body
// stays within a couple of metres of the others, where half precision is about a millimetre), and every
// rotation is compressed to 4 bytes. About 140 bytes for a 13-body character.
public struct PoseFrame : INetworkSerializable
{
    public int Tick;
    // Bumped on every reset, so playback snaps across a teleport instead of sweeping through it.
    public byte ResetCount;
    public Vector3[] Positions;
    public Quaternion[] Rotations;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref ResetCount);

        byte count = (byte)(Positions?.Length ?? 0);
        serializer.SerializeValue(ref count);
        if (serializer.IsReader)
        {
            Positions = new Vector3[count];
            Rotations = new Quaternion[count];
        }
        if (count == 0) return;

        serializer.SerializeValue(ref Positions[0]);
        for (int i = 0; i < count; i++)
        {
            if (i > 0) SerializeOffset(serializer, i);

            uint packedRotation = 0;
            if (serializer.IsWriter)
            {
                Quaternion rotation = Rotations[i];
                packedRotation = QuaternionCompressor.CompressQuaternion(ref rotation);
            }
            serializer.SerializeValue(ref packedRotation);
            if (serializer.IsReader) QuaternionCompressor.DecompressQuaternion(ref Rotations[i], packedRotation);
        }
    }

    void SerializeOffset<T>(BufferSerializer<T> serializer, int i) where T : IReaderWriter
    {
        ushort x = 0, y = 0, z = 0;
        if (serializer.IsWriter)
        {
            Vector3 offset = Positions[i] - Positions[0];
            x = Mathf.FloatToHalf(offset.x);
            y = Mathf.FloatToHalf(offset.y);
            z = Mathf.FloatToHalf(offset.z);
        }
        serializer.SerializeValue(ref x);
        serializer.SerializeValue(ref y);
        serializer.SerializeValue(ref z);
        if (serializer.IsReader)
            Positions[i] = Positions[0] + new Vector3(Mathf.HalfToFloat(x), Mathf.HalfToFloat(y), Mathf.HalfToFloat(z));
    }
}
