public interface IState
{
    void UpdateTick();
    void FixedTick();
    void OnEnter();
    void OnExit();
}