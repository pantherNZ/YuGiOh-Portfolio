
using UnityEngine;

public class Constants : MonoBehaviour
{
    public static Constants Instance { get; private set; }

    private void Start()
    {
        Instance = this;

        storedCardsData = storedCardsDb.text;
    }

    public int SaveGameVersion = 1;
    public bool DownloadImages = true;
    public bool DownloadLargeImages = true;
    public int DefaultStartingNumPages = 10;
    public int DefaultStartingPageWidth = 3;
    public int DefaultStartingPageHeight = 3;

    public Material GreyscaleMaterial;
    public Material BaseCardMaterial;
    public Material SecretRareMaterial;
    public Material UltraRareMaterial;
    public Material GhostRareMaterial;
    public Material SuperRareMaterial;
    public Material PrimasticRareMaterial;
    public Material StarlightRareMaterial;

    [SerializeField] TextAsset storedCardsDb;
    [HideInInspector] public string storedCardsData;
}
