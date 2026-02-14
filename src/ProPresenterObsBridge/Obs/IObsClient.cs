namespace ProPresenterObsBridge.Obs;

public interface IObsClient
{
    bool IsConnected { get; }
    Task ConnectLoopAsync(CancellationToken ct);
    Task<bool> SetProgramSceneAsync(string scene, CancellationToken ct);
    Task<string?> GetCurrentProgramSceneAsync(CancellationToken ct);
}
