namespace EggSharp.GameData.Farm;

public readonly record struct Vec3(float X, float Y, float Z) {
    public static readonly Vec3 Zero = new(0f, 0f, 0f);

    public Vec3 Plus(Vec3 o) => new(X + o.X, Y + o.Y, Z + o.Z);

    public float[] ToArray() => [X, Y, Z];
}
