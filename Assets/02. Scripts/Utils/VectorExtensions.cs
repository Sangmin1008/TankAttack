public static class VectorExtensions
{
    // System.Numerics -> UnityEngine 변환 (받을 때)
    public static UnityEngine.Vector3 ToUnityVector(this System.Numerics.Vector3 v)
    {
        // 주의: System.Numerics는 대문자 X, Y, Z를 사용합니다.
        return new UnityEngine.Vector3(v.X, v.Y, v.Z);
    }

    // UnityEngine -> System.Numerics 변환 (보낼 때)
    public static System.Numerics.Vector3 ToSystemVector(this UnityEngine.Vector3 v)
    {
        // 주의: UnityEngine은 소문자 x, y, z를 사용합니다.
        return new System.Numerics.Vector3(v.x, v.y, v.z);
    }
}