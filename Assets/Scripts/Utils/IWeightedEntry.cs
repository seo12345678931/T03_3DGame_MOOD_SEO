namespace Mood.Utils
{
    /// <summary>
    /// 가중치 기반 랜덤 선택에 필요한 최소 인터페이스.
    /// DropTable의 Entry 클래스들이 구현한다.
    /// </summary>
    public interface IWeightedEntry
    {
        float Weight { get; }
        bool IsValid { get; }
    }
}
