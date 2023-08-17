using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelButton : MonoBehaviour
{
    [SerializeField] private LevelSelect levelSelect;
    [SerializeField] private int level;

    public void SelectLevel()
    {
        levelSelect.SelectLevel(level);
    }
}