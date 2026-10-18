namespace UnityEngine
{
    public enum ColorSpace {Uninitialized=-1,Gamma=0,Linear=1}
    public static class QualitySettings
    {public static ColorSpace activeColorSpace {get;set;}=ColorSpace.Gamma;}
}
