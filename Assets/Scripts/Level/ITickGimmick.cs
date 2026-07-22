namespace AfterYou.Level
{
    /// <summary>
    /// RoundManager의 중앙 틱으로 구동/리셋되는 환경 기믹 계약.
    /// </summary>
    /// <remarks>
    /// 중앙 틱 규약: 클론(ClonePlayback)/박스(PushableBox)와 동일하게 자체 Update/FixedUpdate를 두지 않는다.
    /// RoundManager가 FixedUpdate마다 DriveGimmickTick(tick)을 직접 호출해 하나의 _tick으로 구동한다.
    /// 실시간 API(Time.time / Time.realtimeSinceStartup 등)에 의존하면 재생 결정성이 깨지므로 절대 쓰지 않는다
    /// — 시간이 필요하면 Time.fixedDeltaTime으로 틱 수를 환산해 카운트다운한다.
    /// </remarks>
    public interface ITickGimmick
    {
        /// <summary>라운드/보드 리셋 시 초기 상태로 되돌린다.</summary>
        void ResetGimmick();

        /// <summary>중앙 틱 tick을 이 기믹의 상태로 적용한다.</summary>
        void DriveGimmickTick(int tick);
    }
}
