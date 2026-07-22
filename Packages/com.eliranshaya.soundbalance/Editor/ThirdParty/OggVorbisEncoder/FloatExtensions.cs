using System;

namespace OggVorbisEncoder
{
public static class FloatExtensions
{
    public static float ToDecibel(this float x)
    {
        // Bit-cast float to uint without System.Runtime.CompilerServices.Unsafe
        // (not available on all Unity versions).
        var i = (uint)BitConverter.SingleToInt32Bits(x);
        i &= 0x7fffffff;
        return i * 7.17711438e-7f - 764.6161886f;
    }
}
}
