
using UnityEngine;

public class Constants : MonoBehaviour
{
    public static Constants Instance { get; private set; }

    private void Start()
    {
        Instance = this;
    }

    public int SaveGameVersion = 1;
    public bool DownloadImages = true;
    public bool DownloadLargeImages = true;
    public int DefaultStartingNumPages = 10;
    public int DefaultStartingPageWidth = 3;
    public int DefaultStartingPageHeight = 3;

    public Material greyscaleMaterial;
}
