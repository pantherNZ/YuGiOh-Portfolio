﻿
using UnityEngine;

public class Constants : MonoBehaviour
{
    public static Constants Instance { get; private set; }

    private void Start()
    {
        Instance = this;
    }

    public bool DownloadImages = true;
    public bool DownloadLargeImages = true;
    public int DefaultStartingNumPages = 20;
    public int DefaultStartingPageWidth = 3;
    public int DefaultStartingPageHeight = 3;
}