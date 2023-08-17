using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LevelSelect : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private GameObject resolution;
    [SerializeField] private GameObject levels;
    [SerializeField] private TMP_Text log;

    public void Connected(bool isHost) //called by GameManager
    {
        resolution.SetActive(true);
        if (isHost)
        {
            levels.SetActive(true);
            log.text = "Select a Level";
        }
        else
            log.text = "Waiting for Host";
    }

    public void SelectLevel(int level) //only selectable on server
    {
        NetworkManager.Singleton.SceneManager.LoadScene("Level" + level, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void SelectNewResolution()
    {
        switch (resolutionDropdown.value)
        {
            case 0:
                Screen.SetResolution(1920, 1080, true);
                break;
            case 1:
                Screen.SetResolution(1280, 720, true);
                break;
            case 2:
                Screen.SetResolution(1366, 768, true);
                break;
            case 3:
                Screen.SetResolution(1600, 900, true);
                break;
        }
        PlayerPrefs.SetInt("Resolution", resolutionDropdown.value);
    }
}
