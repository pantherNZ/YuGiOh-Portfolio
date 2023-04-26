
using System;
using UnityEngine;
using UnityEngine.UI;

public class DialogBox : MonoBehaviour
{
    [SerializeField] TMPro.TextMeshProUGUI label;
    [SerializeField] Button confirmBtn;
    [SerializeField] Button cancelBtn;
    [SerializeField] Image border;

    public event Action onConfirm;
    public event Action onCancel;

    TMPro.TextMeshProUGUI confirmBtnLabel;
    TMPro.TextMeshProUGUI cancelBtnLabel;

    private void Start()
    {
        confirmBtn.onClick.AddListener( () =>
        {
            gameObject.SetActive( false );
            onConfirm?.Invoke();
        } );

        cancelBtn.onClick.AddListener( () =>
        {
            gameObject.SetActive( false );
            onCancel?.Invoke();
        } );

        confirmBtnLabel = confirmBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        cancelBtnLabel = cancelBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
    }

    public void SetMessage( string msg )
    {
        label.text = msg;
    }

    public void SetButtonLabels( string confirmText, string cancelText )
    {
        confirmBtnLabel.text = confirmText;
        cancelBtnLabel.text = cancelText;
    }

    public void SetBorderColour( Color col )
    {
        border.color = col;
    }
}