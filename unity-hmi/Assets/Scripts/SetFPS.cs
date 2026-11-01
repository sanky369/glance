using UnityEngine;

public class SetFPS : MonoBehaviour
{
    public int targetFramerate = 60;
    
    private void Awake()
    {
        Application.targetFrameRate = targetFramerate;
    }
}