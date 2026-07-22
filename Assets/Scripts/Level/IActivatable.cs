namespace AfterYou.Level
{
    /// <summary>
    /// 스위치 → 대상(문/발판) 배선의 수신 계약. 한 스위치가 여러 대상을 동시에 켜고 끌 수 있다(다대상).
    /// </summary>
    public interface IActivatable
    {
        /// <summary>활성/비활성 상태를 통지받는다. isActive=true면 켜짐(문 열림/발판 가동).</summary>
        void SetActivated(bool isActive);

        /// <summary>현재 활성 상태. 스위치가 대상의 자동 닫힘을 감지해 자기 상태를 동기화하는 데 쓴다.</summary>
        bool IsActivated { get; }
    }
}
