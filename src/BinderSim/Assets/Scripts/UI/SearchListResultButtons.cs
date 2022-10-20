using UnityEngine;
using UnityEngine.UI;

public class SearchListResultButtons : MonoBehaviour
{
    [SerializeField] Button addToInventoryButton;
    [SerializeField] Button addToBinderButton;
    [SerializeField] Button removeButton;

    public Button AddToInventoryButton { get => addToInventoryButton; private set { } }
    public Button AddToBinderButton { get => addToBinderButton; private set { } }
    public Button RemoveButton { get => removeButton; private set { } }
}