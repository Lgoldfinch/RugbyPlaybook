/// <summary>
/// Holds the play selected on the home screen so the pitch scene can load it after <see cref="UnityEngine.SceneManagement.SceneManager.LoadScene"/>.
/// </summary>
public static class PlayNavContext
{
    public static Play CurrentPlay { get; set; }
}
