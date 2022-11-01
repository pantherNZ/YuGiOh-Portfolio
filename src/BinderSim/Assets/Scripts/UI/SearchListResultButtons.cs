using UnityEngine;
using UnityEngine.UI;

public class SearchListResultButtons : MonoBehaviour
{
    [SerializeField] Button addToInventoryButton;
    [SerializeField] Button addToBinderButton;
    [SerializeField] Button removeFromInventoryButton;
    [SerializeField] Button removeFromBinderButton;

    public Button AddToInventoryButton { get => addToInventoryButton; private set { } }
    public Button AddToBinderButton { get => addToBinderButton; private set { } }
    public Button RemoveFromInventoryButton { get => removeFromInventoryButton; private set { } }
    public Button RemoveFromBinderButton { get => removeFromBinderButton; private set { } }
}