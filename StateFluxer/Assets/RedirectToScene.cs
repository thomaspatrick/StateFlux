using UnityEngine;
using UnityEngine.SceneManagement;

public class RedirectToScene : MonoBehaviour
{
    public Scene scene;

    void Awake()
    {
        //SceneManager.LoadScene("LobbyScene",LoadSceneMode.Additive);
        SceneManager.LoadScene("LobbyScene");
    }
}
